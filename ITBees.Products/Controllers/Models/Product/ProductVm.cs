namespace ITBees.Products.Controllers.Models.Product;

public class ProductVm
{
    public ProductVm(){}

    public ProductVm(DbModels.Product x)
    {
        Id = x.Id;
        ProducerId = x.ProducerId;
        Producer = x.Producer == null ? null : new ProducerVm(x.Producer);
        ProductImages = x.ProductImages == null ? null : x.ProductImages.Select(y => new ProductImageVm(y)).ToList();
        ThumbnailUrl = x.ThumbnailUrl;
        ShortDescription = x.ShortDescription;
        LongDescription = x.LongDescription;
        Created = x.Created;
        IsActive = x.IsActive;
        NetPriceSell = x.NetPriceSell;
        VatPercentageSell = x.VatPercentageSell;
        GrossPriceSell = ProductPrices.EffectiveGross(x);
        IsPubliclyAvailable = x.IsPubliclyAvailable;
        OrderFulfillmentDays = x.OrderFulfillmentDays;
        NetPriceBuy = x.NetPriceBuy;
        VatPercentageBuy = x.VatPercentageBuy;
        Ean = x.Ean;
        WithoutSerialNumbers = x.WithoutSerialNumbers;
        AddedByGuid = x.AddedByGuid;

    }
    
    public int Id { get; set; }
    public ProducerVm Producer { get; set; }
    public int ProducerId { get; set; }
    public List<ProductImageVm> ProductImages { get; set; }
    public string ThumbnailUrl { get; set; }
    public string ShortDescription { get; set; }
    public string LongDescription { get; set; }
    public DateTime Created { get; set; }
    public bool IsActive { get; set; }
    public decimal NetPriceSell { get; set; }
    public int VatPercentageSell { get; set; }

    /// <summary>Gross sale price of one piece (for rows older than the field: the net price with VAT).</summary>
    public decimal GrossPriceSell { get; set; }

    /// <summary>"Publicznie dostępny" - shown to customers of the host's public shop.</summary>
    public bool IsPubliclyAvailable { get; set; }

    /// <summary>"Termin realizacji zamówienia" in working days when not on stock; null = none set.</summary>
    public int? OrderFulfillmentDays { get; set; }

    public decimal NetPriceBuy { get; set; }
    public int VatPercentageBuy { get; set; }
    public string Ean { get; set; }

    /// <summary>Kept on stock as a quantity, counted by scanning <see cref="Ean"/> - not item by item.</summary>
    public bool WithoutSerialNumbers { get; set; }

    public Guid AddedByGuid { get; set; }
}