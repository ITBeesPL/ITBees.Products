using ITBees.Products.DbModels;
using ITBees.Products.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITBees.Products.Setup;

public class DbModelBuilder
{
    /// <summary>
    /// Registers every entity shipped with this package. Call it from the consuming
    /// DbContext's OnModelCreating - the host must not repeat these mappings itself.
    /// </summary>
    public static void Register(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Warehouse>().HasKey(x => x.Guid);
        modelBuilder.Entity<Producer>().HasKey(x => x.Id);
        modelBuilder.Entity<Product>().HasKey(x => x.Id);
        modelBuilder.Entity<ProductImage>().HasKey(x => x.Id);
        modelBuilder.Entity<SimCard>().HasKey(x => x.Id);
        modelBuilder.Entity<SimCardOperator>().HasKey(x => x.Id);
        modelBuilder.Entity<SerializedProductOnStock>().HasKey(x => x.Id);

        // Stock must never vanish as a side effect. By convention both relations would
        // cascade, so physically deleting the user account that once added a product would
        // silently take the product and every item of it on stock along.
        modelBuilder.Entity<Product>()
            .HasOne(x => x.AddedBy)
            .WithMany()
            .HasForeignKey(x => x.AddedByGuid)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SerializedProductOnStock>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // The internal serial number is what a scanned label resolves to. It is nullable so
        // that rows stored before it existed do not collide on the unique index.
        modelBuilder.Entity<SerializedProductOnStock>().HasIndex(x => x.Guid).IsUnique();

        // Purchase batches - listed newest first; items are removed explicitly together with
        // their delivery, never by a database cascade.
        modelBuilder.Entity<ProductDelivery>().HasKey(x => x.Guid);
        modelBuilder.Entity<ProductDelivery>().HasIndex(x => x.PurchaseDate);
        modelBuilder.Entity<SerializedProductOnStock>()
            .HasOne(x => x.ProductDelivery)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.ProductDeliveryGuid)
            .OnDelete(DeleteBehavior.Restrict);

        // Stock of the products kept without serial numbers: a ledger summed per product and
        // warehouse. Like the serialized items, movements are only ever removed explicitly,
        // together with the document that made them - never by a database cascade.
        modelBuilder.Entity<ProductStockMovement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProductId, x.WarehouseGuid });
            entity.HasIndex(x => x.DocumentGuid);
            entity.Property(x => x.DocumentName).HasMaxLength(200);
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseGuid)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProductDelivery)
                .WithMany(x => x.StockMovements)
                .HasForeignKey(x => x.ProductDeliveryGuid)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
