namespace Billing.Api.Messaging;

public class InvoiceClosedEvent
{
    public Guid InvoiceId { get; set; }
    public int InvoiceNumber { get; set; }
    public DateTime ClosedAt { get; set; }
}
