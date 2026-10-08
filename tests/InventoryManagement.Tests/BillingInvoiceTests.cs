using System.Net;
using System.Text;
using Billing.Api.Clients;
using Billing.Api.Controllers;
using Billing.Api.Data;
using Billing.Api.Messaging;
using Billing.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagement.Tests;

public class BillingInvoiceTests
{
    [Fact]
    public async Task Create_StartsAsOpen()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, StubHandler.Always(HttpStatusCode.OK));

        var result = await controller.Create(NewCreateRequest(), CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var invoice = Assert.IsType<Invoice>(created.Value);

        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Equal(1, invoice.Number);
        Assert.Single(invoice.Items);
    }

    [Fact]
    public async Task Issue_WhenInventorySucceeds_ClosesInvoice()
    {
        await using var db = CreateDb();
        var invoice = await SeedOpenInvoice(db);
        var handler = StubHandler.Always(HttpStatusCode.OK);
        var controller = CreateController(db, handler);

        var result = await controller.Issue(invoice.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var closed = Assert.IsType<Invoice>(ok.Value);
        Assert.Equal(InvoiceStatus.Closed, closed.Status);
        Assert.Equal(InvoiceStatus.Closed, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Issue_WhenAlreadyClosed_ReturnsConflict()
    {
        await using var db = CreateDb();
        var invoice = await SeedOpenInvoice(db);
        invoice.Status = InvoiceStatus.Closed;
        await db.SaveChangesAsync();
        var handler = StubHandler.Always(HttpStatusCode.OK);
        var controller = CreateController(db, handler);

        var result = await controller.Issue(invoice.Id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(InvoiceStatus.Closed, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Issue_WhenInventoryFails_KeepsInvoiceOpen()
    {
        await using var db = CreateDb();
        var invoice = await SeedOpenInvoice(db);
        var handler = StubHandler.AlwaysThrow(new HttpRequestException("Connection refused"));
        var controller = CreateController(db, handler);

        var result = await controller.Issue(invoice.Id, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
        Assert.Equal(InvoiceStatus.Open, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Issue_WhenTransientFailureThenSuccess_RetriesAndClosesInvoice()
    {
        await using var db = CreateDb();
        var invoice = await SeedOpenInvoice(db);
        var handler = StubHandler.Sequence(
            () => throw new HttpRequestException("Connection refused"),
            () => StubHandler.Json(HttpStatusCode.OK));
        var controller = CreateController(db, handler);

        var result = await controller.Issue(invoice.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(InvoiceStatus.Closed, Assert.IsType<Invoice>(ok.Value).Status);
        Assert.Equal(InvoiceStatus.Closed, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Issue_WhenInsufficientStock_DoesNotRetry()
    {
        await using var db = CreateDb();
        var invoice = await SeedOpenInvoice(db);
        var handler = StubHandler.Always(HttpStatusCode.UnprocessableEntity);
        var controller = CreateController(db, handler);

        var result = await controller.Issue(invoice.Id, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, status.StatusCode);
        Assert.Equal(InvoiceStatus.Open, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(1, handler.Calls);
    }

    private static InvoicesController CreateController(BillingDbContext db, StubHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://inventory.test/")
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:HostName"] = "127.0.0.1",
            ["RabbitMQ:Port"] = "1"
        }).Build();

        return new InvoicesController(
            db,
            new InventoryClient(http),
            new InvoiceClosedPublisher(config, NullLogger<InvoiceClosedPublisher>.Instance));
    }

    private static TestBillingDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CreateInvoiceRequest NewCreateRequest() => new()
    {
        Items =
        [
            new CreateInvoiceItemRequest
            {
                ProductId = Guid.NewGuid(),
                ProductCode = "P1",
                ProductDescription = "Produto",
                Quantity = 2
            }
        ]
    };

    private static async Task<Invoice> SeedOpenInvoice(BillingDbContext db)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = 1,
            Status = InvoiceStatus.Open,
            CreatedAt = DateTime.UtcNow,
            Items =
            [
                new InvoiceItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    ProductCode = "P1",
                    ProductDescription = "Produto",
                    Quantity = 1
                }
            ]
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    private sealed class TestBillingDbContext : BillingDbContext
    {
        private int _number;

        public TestBillingDbContext(DbContextOptions<BillingDbContext> options) : base(options)
        {
        }

        public override Task<int> NextInvoiceNumberAsync(CancellationToken cancellationToken) =>
            Task.FromResult(++_number);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public int Calls { get; private set; }

        private StubHandler(IEnumerable<Func<HttpResponseMessage>> responses)
        {
            foreach (var response in responses)
            {
                _responses.Enqueue(response);
            }
        }

        public static StubHandler Always(HttpStatusCode status) =>
            new([() => Json(status)]);

        public static StubHandler AlwaysThrow(Exception exception) =>
            new([() => throw exception]);

        public static StubHandler Sequence(params Func<HttpResponseMessage>[] responses) =>
            new(responses);

        public static HttpResponseMessage Json(HttpStatusCode status) =>
            new(status)
            {
                Content = new StringContent("""{"status":"debited","error":"Insufficient stock."}""", Encoding.UTF8, "application/json")
            };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var next = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
            return Task.FromResult(next());
        }
    }
}
