namespace ITBees.Products.Controllers.Models.Product;

public class ProductUm
{
    public int ProductId { get; set; }
    public string Ean { get; set; }
    public List<ProductImageVm> ProductImages { get; set; }
    public ProductImageVm Thumbnail { get; set; }
    public int ProducerId { get; set; }
    public string Model { get; set; }
    public string DescriptionUrl { get; set; }
    public string ShortDescription { get; set; }
    public string LongDescription { get; set; }
    public decimal NetPriceSell { get; set; }
    public int VatPercentageSell { get; set; }
    public decimal NetPriceBuy { get; set; }
    public int VatPercentageBuy { get; set; }
}