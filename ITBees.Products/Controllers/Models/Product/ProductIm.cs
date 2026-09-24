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

    /// <summary>
    /// Gross sale price of one piece; null = the net price with VAT. When given it has to agree
    /// with the net price and the VAT rate to within one grosz (see ProductPrices).
    /// </summary>
    public decimal? GrossPriceSell { get; set; }

    /// <summary>"Publicznie dostępny" - the host may show the product to customers and let them order it.</summary>
    public bool IsPubliclyAvailable { get; set; }

    /// <summary>"Termin realizacji zamówienia" in working days when not on stock; null or 0 = none.</summary>
    public int? OrderFulfillmentDays { get; set; }
    public decimal NetPriceBuy { get; set; }
    public int VatPercentageBuy { get; set; }

    /// <summary>Kept on stock as a quantity, counted by scanning the EAN code - see DbModels.Product.</summary>
    public bool WithoutSerialNumbers { get; set; }
}
