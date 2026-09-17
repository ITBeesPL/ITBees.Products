namespace ITBees.Products.Controllers.Models.Product;

public class ProductIm
{
    // Everything optional is declared nullable on purpose: the hosts compile with nullable
    // reference types enabled, where MVC treats a non-nullable reference property of a request
    // body as [Required] and answers 400 as soon as a client leaves it out.
    public string? Ean { get; set; }
    public List<ProductImageIm>? ProductImages { get; set; }
    public ProductImageIm? Thumbnail { get; set; }
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
