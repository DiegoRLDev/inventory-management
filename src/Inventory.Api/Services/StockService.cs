using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Inventory.Api.Services;

public class StockService
{
    private readonly InventoryDbContext _db;

    public StockService(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<DebitStockOutcome> DebitAsync(DebitStockRequest request, CancellationToken cancellationToken)
    {
        if (await AlreadyProcessedAsync(request.InvoiceId, cancellationToken))
        {
            return DebitStockOutcome.Ok();
        }

        var quantitiesByProduct = request.Items
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        IDbContextTransaction? transaction = null;
        if (_db.Database.IsRelational())
        {
            transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            if (await AlreadyProcessedAsync(request.InvoiceId, cancellationToken))
            {
                return DebitStockOutcome.Ok();
            }

            foreach (var (productId, quantity) in quantitiesByProduct)
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
                if (product is null)
                {
                    return DebitStockOutcome.Fail(StatusCodes.Status404NotFound, "Product not found.", productId);
                }

                if (product.Balance < quantity)
                {
                    return DebitStockOutcome.Fail(
                        StatusCodes.Status422UnprocessableEntity,
                        "Insufficient stock.",
                        productId,
                        product.Balance,
                        quantity);
                }

                product.Balance -= quantity;
            }

            _db.ProcessedDebits.Add(new ProcessedDebit
            {
                Id = Guid.NewGuid(),
                InvoiceId = request.InvoiceId,
                ProcessedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return DebitStockOutcome.Ok();
        }
        catch (DbUpdateException ex) when (IsDuplicateInvoiceId(ex))
        {
            return DebitStockOutcome.Ok();
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private Task<bool> AlreadyProcessedAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        _db.ProcessedDebits.AnyAsync(debit => debit.InvoiceId == invoiceId, cancellationToken);

    private static bool IsDuplicateInvoiceId(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

public sealed class DebitStockOutcome
{
    public bool Succeeded { get; private init; }
    public int StatusCode { get; private init; }
    public string? Error { get; private init; }
    public Guid? ProductId { get; private init; }
    public int? Available { get; private init; }
    public int? Requested { get; private init; }

    public static DebitStockOutcome Ok() => new() { Succeeded = true, StatusCode = StatusCodes.Status200OK };

    public static DebitStockOutcome Fail(
        int statusCode,
        string error,
        Guid productId,
        int? available = null,
        int? requested = null) =>
        new()
        {
            Succeeded = false,
            StatusCode = statusCode,
            Error = error,
            ProductId = productId,
            Available = available,
            Requested = requested
        };
}
