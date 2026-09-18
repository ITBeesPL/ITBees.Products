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

    /// <summary>
    /// Kept on stock as a quantity per warehouse instead of one row per serial number - e.g.
    /// cables, paper rolls or spare parts. Such pieces are counted by scanning the <see cref="Ean"/>
    /// code on them and their stock is a sum of <see cref="Entities.ProductStockMovement"/> rows.
    /// False (the default) keeps the product serialized, as every product was before this flag.
    /// </summary>
    public bool WithoutSerialNumbers { get; set; }

    public UserAccount AddedBy { get; set; }
    public Guid AddedByGuid { get; set; }
}