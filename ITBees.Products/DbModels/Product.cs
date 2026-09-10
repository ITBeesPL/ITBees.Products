using ITBees.Models.Users;
using ITBees.Products.Entities;

namespace ITBees.Products.DbModels;

public class Product
{
    public int Id { get; set; }
    public Producer Producer { get; set; }
    public int ProducerId { get; set; }
    public List<ProductImage> ProductImages { get; set; }
    public string ThumbnailUrl { get; set; }
    public string ShortDescription { get; set; }
    public string LongDescription { get; set; }
    public DateTime Created { get; set; }
    public bool IsActive { get; set; }
    public decimal NetPriceSell { get; set; }
    public int VatPercentageSell { get; set; }
    public decimal NetPriceBuy { get; set; }
    public int VatPercentageBuy { get; set; }
    public string Ean { get; set; }
    public UserAccount AddedBy { get; set; }
    public Guid AddedByGuid { get; set; }
}