using ITBees.Products.Entities;

namespace ITBees.Products.Controllers.Models.ProductGroups;

public class ProductGroupVm
{
    public ProductGroupVm()
    {
    }

    public ProductGroupVm(ProductGroup x, int productCount)
    {
        Id = x.Id;
        Name = x.Name;
        Description = x.Description;
        Created = x.Created;
        ProductCount = productCount;
    }

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Created { get; set; }

    /// <summary>Catalogue entries in the group, archived (inactive) ones included.</summary>
    public int ProductCount { get; set; }
}
