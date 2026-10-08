using Inventory.Api.Models;
using Inventory.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/stock")]
public class StockController : ControllerBase
{
    private readonly StockService _stockService;

    public StockController(StockService stockService)
    {
        _stockService = stockService;
    }

    [HttpPost("debit")]
    public async Task<IActionResult> Debit(DebitStockRequest request, CancellationToken cancellationToken)
    {
        var result = await _stockService.DebitAsync(request, cancellationToken);
        if (result.Succeeded)
        {
            return Ok(new { status = "debited" });
        }

        return StatusCode(result.StatusCode, new
        {
            error = result.Error,
            productId = result.ProductId,
            available = result.Available,
            requested = result.Requested
        });
    }
}
