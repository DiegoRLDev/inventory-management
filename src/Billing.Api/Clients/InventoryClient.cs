using System.Net.Http.Json;
using Billing.Api.Models;

namespace Billing.Api.Clients;

public class InventoryClient
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly HttpClient _http;

    public InventoryClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<InventoryDebitResult> DebitAsync(
        Guid invoiceId,
        IEnumerable<InvoiceItem> items,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            invoiceId,
            items = items.Select(item => new
            {
                productId = item.ProductId,
                quantity = item.Quantity
            })
        };

        InventoryDebitResult? lastFailure = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync("api/stock/debit", payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return InventoryDebitResult.Ok();
                }

                var status = (int)response.StatusCode;
                if (IsTransientStatus(status) && attempt < MaxAttempts)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                    continue;
                }

                var body = await response.Content.ReadFromJsonAsync<InventoryErrorResponse>(cancellationToken);
                var message = status switch
                {
                    StatusCodes.Status404NotFound => "Product not found in inventory.",
                    StatusCodes.Status422UnprocessableEntity => "Insufficient stock.",
                    _ => "Inventory rejected the stock debit."
                };

                return InventoryDebitResult.Fail(status, message, body?.ProductId, body?.Available, body?.Requested);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastFailure = InventoryDebitResult.Fail(
                    StatusCodes.Status504GatewayTimeout,
                    "Inventory service timed out.");
            }
            catch (HttpRequestException)
            {
                lastFailure = InventoryDebitResult.Fail(
                    StatusCodes.Status503ServiceUnavailable,
                    "Inventory service is unavailable.");
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        return lastFailure ?? InventoryDebitResult.Fail(
            StatusCodes.Status503ServiceUnavailable,
            "Inventory service is unavailable.");
    }

    private static bool IsTransientStatus(int status) => status >= 500 && status <= 599;

    private sealed class InventoryErrorResponse
    {
        public string? Error { get; set; }
        public Guid? ProductId { get; set; }
        public int? Available { get; set; }
        public int? Requested { get; set; }
    }
}

public sealed class InventoryDebitResult
{
    public bool Succeeded { get; private init; }
    public int StatusCode { get; private init; }
    public string? Error { get; private init; }
    public Guid? ProductId { get; private init; }
    public int? Available { get; private init; }
    public int? Requested { get; private init; }

    public static InventoryDebitResult Ok() =>
        new() { Succeeded = true, StatusCode = StatusCodes.Status200OK };

    public static InventoryDebitResult Fail(
        int statusCode,
        string error,
        Guid? productId = null,
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
