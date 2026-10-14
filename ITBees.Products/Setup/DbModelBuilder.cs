using ITBees.Products.DbModels;
using ITBees.Products.Entities;
using ITBees.Products.Services.ProductGroups;
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

        // A service may have no producer, so the relation is optional - and restricted like the
        // ones below: producers are deactivated, never deleted together with their products.
        modelBuilder.Entity<Product>()
            .HasOne(x => x.Producer)
            .WithMany()
            .HasForeignKey(x => x.ProducerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Groups ("Wykłady", "Akcesoria"...) - a product belongs to any number of them. Hosts look
        // a group up by its name, so the name is unique. Memberships are plain links: they go
        // together with the group or the product they join.
        modelBuilder.Entity<ProductGroup>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(ProductGroupLimits.MaxNameLength).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(ProductGroupLimits.MaxDescriptionLength);
            entity.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<ProductInGroup>(entity =>
        {
            entity.HasKey(x => new { x.ProductId, x.ProductGroupId });
            entity.HasIndex(x => x.ProductGroupId);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.ProductGroups)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ProductGroup)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.ProductGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Trial products taken by customers - one row per product and customer; the unique index is
        // what refuses a second claim of the same trial, also from two requests at once. Products are
        // archived, never deleted, so a used trial keeps its product.
        modelBuilder.Entity<ProductTrialUse>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProductId, x.CustomerGuid }).IsUnique();
            entity.HasIndex(x => x.CustomerGuid);
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

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
