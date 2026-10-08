using Inventory.Api.Data;
using Inventory.Api.Models;
using Inventory.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Tests;

public class InventoryStockTests
{
    [Fact]
    public async Task Debit_WithInsufficientBalance_DoesNotChangeStock()
    {
        var database = Guid.NewGuid().ToString();
        var product = NewProduct(balance: 1);
        await using (var db = CreateDb(database, product))
        {
            var result = await new StockService(db).DebitAsync(Request(product.Id, 5), CancellationToken.None);
            Assert.False(result.Succeeded);
            Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        }

        await using var verify = CreateDb(database);
        Assert.Equal(1, (await verify.Products.SingleAsync()).Balance);
        Assert.Empty(verify.ProcessedDebits);
    }

    [Fact]
    public async Task Debit_WithEnoughBalance_ReducesStock()
    {
        var database = Guid.NewGuid().ToString();
        var product = NewProduct(balance: 10);
        await using (var db = CreateDb(database, product))
        {
            var result = await new StockService(db).DebitAsync(Request(product.Id, 4), CancellationToken.None);
            Assert.True(result.Succeeded);
        }

        await using var verify = CreateDb(database);
        Assert.Equal(6, (await verify.Products.SingleAsync()).Balance);
        Assert.Equal(1, await verify.ProcessedDebits.CountAsync());
    }

    [Fact]
    public async Task Debit_SecondCallWithSameInvoiceId_DoesNotChangeStockAgain()
    {
        var database = Guid.NewGuid().ToString();
        var product = NewProduct(balance: 10);
        var invoiceId = Guid.NewGuid();

        await using (var db = CreateDb(database, product))
        {
            var service = new StockService(db);
            Assert.True((await service.DebitAsync(Request(product.Id, 4, invoiceId), CancellationToken.None)).Succeeded);
            Assert.True((await service.DebitAsync(Request(product.Id, 4, invoiceId), CancellationToken.None)).Succeeded);
        }

        await using var verify = CreateDb(database);
        Assert.Equal(6, (await verify.Products.SingleAsync()).Balance);
        Assert.Equal(1, await verify.ProcessedDebits.CountAsync());
        Assert.Equal(invoiceId, (await verify.ProcessedDebits.SingleAsync()).InvoiceId);
    }

    [Fact]
    public async Task Debit_WhenSecondItemFails_DoesNotChangeAnyProduct()
    {
        var database = Guid.NewGuid().ToString();
        var first = NewProduct(balance: 10);
        var second = NewProduct(balance: 1);
        await using (var db = CreateDb(database, first, second))
        {
            var result = await new StockService(db).DebitAsync(
                new DebitStockRequest
                {
                    InvoiceId = Guid.NewGuid(),
                    Items =
                    [
                        new DebitStockItemRequest { ProductId = first.Id, Quantity = 2 },
                        new DebitStockItemRequest { ProductId = second.Id, Quantity = 8 }
                    ]
                },
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        }

        await using var verify = CreateDb(database);
        var stored = await verify.Products.OrderBy(product => product.Balance).ToListAsync();
        Assert.Equal(1, stored[0].Balance);
        Assert.Equal(10, stored[1].Balance);
        Assert.Empty(verify.ProcessedDebits);
    }

    private static Product NewProduct(int balance) => new()
    {
        Id = Guid.NewGuid(),
        Code = Guid.NewGuid().ToString("N")[..8],
        Description = "Produto teste",
        Balance = balance
    };

    private static DebitStockRequest Request(Guid productId, int quantity, Guid? invoiceId = null) => new()
    {
        InvoiceId = invoiceId ?? Guid.NewGuid(),
        Items = [new DebitStockItemRequest { ProductId = productId, Quantity = quantity }]
    };

    private static InventoryDbContext CreateDb(string database, params Product[] products)
    {
        var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(database)
            .Options);

        if (products.Length > 0)
        {
            db.Products.AddRange(products);
            db.SaveChanges();
        }

        return db;
    }
}
