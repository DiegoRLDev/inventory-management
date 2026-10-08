namespace Inventory.Api.Models;

public class ProcessedDebit
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
