namespace ITBees.Products.Entities;

/// <summary>
/// A named set of catalogue entries, e.g. "Wykłady" - a product may belong to any number of groups.
/// Hosts use groups to decide what to offer where (a shop page, a section of a panel), so the name
/// is unique.
/// </summary>
public class ProductGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Created { get; set; }
    public Guid? CreatedByGuid { get; set; }
    public List<ProductInGroup>? Products { get; set; }
}
