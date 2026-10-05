namespace ITBees.Products.Entities;

/// <summary>Membership of a product in a <see cref="ProductGroup"/>.</summary>
public class ProductInGroup
{
    public DbModels.Product? Product { get; set; }
    public int ProductId { get; set; }

    public ProductGroup? ProductGroup { get; set; }
    public int ProductGroupId { get; set; }
}
