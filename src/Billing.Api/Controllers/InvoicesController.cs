using Billing.Api.Clients;
using Billing.Api.Data;
using Billing.Api.Messaging;
using Billing.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Billing.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly BillingDbContext _db;
    private readonly InventoryClient _inventory;
    private readonly InvoiceClosedPublisher _publisher;

    public InvoicesController(BillingDbContext db, InventoryClient inventory, InvoiceClosedPublisher publisher)
    {
        _db = db;
        _inventory = inventory;
        _publisher = publisher;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Invoice>>> GetAll(CancellationToken cancellationToken)
    {
        var invoices = await _db.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Items)
            .OrderBy(invoice => invoice.Number)
            .ToListAsync(cancellationToken);

        return Ok(invoices);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Invoice>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Items)
            .FirstOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        return Ok(invoice);
    }

    [HttpPost]
    public async Task<ActionResult<Invoice>> Create(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = await _db.NextInvoiceNumberAsync(cancellationToken),
            Status = InvoiceStatus.Open,
            CreatedAt = DateTime.UtcNow,
            Items = request.Items.Select(item => new InvoiceItem
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                ProductCode = item.ProductCode.Trim(),
                ProductDescription = item.ProductDescription.Trim(),
                Quantity = item.Quantity
            }).ToList()
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<IActionResult> Issue(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
            .Include(invoice => invoice.Items)
            .FirstOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Status == InvoiceStatus.Closed)
        {
            return Conflict(new { error = "Invoice is already closed." });
        }

        var debit = await _inventory.DebitAsync(invoice.Id, invoice.Items, cancellationToken);
        if (!debit.Succeeded)
        {
            return StatusCode(debit.StatusCode, new
            {
                error = debit.Error,
                productId = debit.ProductId,
                available = debit.Available,
                requested = debit.Requested
            });
        }

        invoice.Status = InvoiceStatus.Closed;
        await _db.SaveChangesAsync(cancellationToken);

        await _publisher.PublishAsync(
            new InvoiceClosedEvent
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.Number,
                ClosedAt = DateTime.UtcNow
            },
            cancellationToken);

        return Ok(invoice);
    }
}
