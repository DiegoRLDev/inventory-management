using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.Models;

public class DebitStockRequest
{
    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    [MinLength(1)]
    public List<DebitStockItemRequest> Items { get; set; } = [];
}

public class DebitStockItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
