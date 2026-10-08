namespace Billing.Api.Models;

public class Invoice
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<InvoiceItem> Items { get; set; } = [];
}
