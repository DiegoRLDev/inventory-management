using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProcessedDebit> ProcessedDebits => Set<ProcessedDebit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Code).IsRequired().HasMaxLength(50);
            entity.Property(product => product.Description).IsRequired().HasMaxLength(200);
            entity.HasIndex(product => product.Code).IsUnique();
        });

        modelBuilder.Entity<ProcessedDebit>(entity =>
        {
            entity.HasKey(debit => debit.Id);
            entity.HasIndex(debit => debit.InvoiceId).IsUnique();
        });
    }
}
