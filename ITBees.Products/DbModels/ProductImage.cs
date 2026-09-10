using ITBees.Products.DbModels;

namespace ITBees.Products.Entities;

public class ProductImage
{
    public int Id { get; set; }
    public Product Product { get; set; }
    public int ProductId { get; set; }
    public string ImageUrl { get; set; }
}