namespace ITBees.Products.Controllers.Models.Product;

public class ProductUm
{
    public int ProductId { get; set; }

    // Optional members are nullable on purpose - see ProductIm.
    public string? Ean { get; set; }

    /// <summary>Replaces the stored images; null leaves them untouched.</summary>
    public List<ProductImageVm>? ProductImages { get; set; }

    /// <summary>Replaces the stored thumbnail; null leaves it untouched.</summary>
    public ProductImageVm? Thumbnail { get; set; }
    public int ProducerId { get; set; }
    public string? Model { get; set; }
    public string? DescriptionUrl { get; set; }
    public string ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public decimal NetPriceSell { get; set; }
    public int VatPercentageSell { get; set; }
    public decimal NetPriceBuy { get; set; }
    public int VatPercentageBuy { get; set; }
}
