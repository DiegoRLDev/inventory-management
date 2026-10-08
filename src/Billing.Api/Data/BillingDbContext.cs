using Billing.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Billing.Api.Data;

public class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>("invoice_numbers").StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(invoice => invoice.Id);
            entity.Property(invoice => invoice.Number).IsRequired();
            entity.Property(invoice => invoice.Status).IsRequired();
            entity.Property(invoice => invoice.CreatedAt).IsRequired();
            entity.HasMany(invoice => invoice.Items)
                .WithOne()
                .HasForeignKey(item => item.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ProductCode).IsRequired().HasMaxLength(50);
            entity.Property(item => item.ProductDescription).IsRequired().HasMaxLength(200);
        });
    }

    public virtual async Task<int> NextInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        return await Database
            .SqlQueryRaw<int>("SELECT nextval('invoice_numbers')::int AS \"Value\"")
            .SingleAsync(cancellationToken);
    }
}
