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
    }
}