using ITBees.Products.Controllers.Models.ProductGroups;
using ITBees.Products.Entities;

namespace ITBees.Products.Controllers.Models.Product;

public class ProductVm
{
    public ProductVm(){}

    /// <param name="groups">
    /// Groups the product belongs to; without them <see cref="ProductGroups"/> stays empty (the
    /// membership rows do not carry the group names).
    /// </param>
    public ProductVm(DbModels.Product x, IEnumerable<ProductGroup>? groups = null)
    {
        Id = x.Id;
        ProducerId = x.ProducerId;
        Kind = x.Kind;
        ValidityPeriod = x.ValidityPeriod;
        ValidityUnit = x.ValidityUnit;
        ValidityDescription = ProductValidity.Describe(x);
        ProductGroups = (groups ?? Enumerable.Empty<ProductGroup>())
            .OrderBy(g => g.Name)
            .Select(g => new ProductGroupRefVm { Id = g.Id, Name = g.Name })
            .ToList();
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
    public ProducerVm? Producer { get; set; }

    /// <summary>Null only for a service without a producer.</summary>
    public int? ProducerId { get; set; }

    public ProductKind Kind { get; set; }

    /// <summary>Services: how long a bought service stays valid, in <see cref="ValidityUnit"/>s; null = no end date.</summary>
    public int? ValidityPeriod { get; set; }

    public ProductValidityUnit? ValidityUnit { get; set; }

    /// <summary>The validity in Polish words, e.g. "12 miesięcy"; null when there is none.</summary>
    public string? ValidityDescription { get; set; }

    public List<ProductGroupRefVm> ProductGroups { get; set; } = new();
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