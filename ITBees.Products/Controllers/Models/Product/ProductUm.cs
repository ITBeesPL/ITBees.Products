using ITBees.Products.Entities;

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

    /// <summary>Required for goods; a service may have none (null or 0).</summary>
    public int? ProducerId { get; set; }

    /// <summary>Goods or a service / subscription; null leaves it untouched.</summary>
    public ProductKind? Kind { get; set; }

    /// <summary>
    /// Services only: how long a bought service stays valid, in <see cref="ValidityUnit"/>s; null
    /// leaves it untouched, 0 clears it (no end date).
    /// </summary>
    public int? ValidityPeriod { get; set; }

    /// <summary>Unit of <see cref="ValidityPeriod"/>; null keeps the stored one.</summary>
    public ProductValidityUnit? ValidityUnit { get; set; }

    /// <summary>Replaces the groups of the product (ProductGroup ids); null leaves them untouched.</summary>
    public List<int>? ProductGroupIds { get; set; }

    /// <summary>False archives the product (hidden from the default list), true restores it; null leaves it untouched.</summary>
    public bool? IsActive { get; set; }
    public string? Model { get; set; }
    public string? DescriptionUrl { get; set; }
    public string ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public decimal NetPriceSell { get; set; }
    public int VatPercentageSell { get; set; }

    /// <summary>
    /// Gross sale price of one piece. Null = the net price with VAT, so a client that does not
    /// know the field keeps the gross price in step with the net price it edits.
    /// </summary>
    public decimal? GrossPriceSell { get; set; }

    /// <summary>"Publicznie dostępny"; null leaves it untouched, so older clients never switch it off.</summary>
    public bool? IsPubliclyAvailable { get; set; }

    /// <summary>
    /// "Termin realizacji zamówienia" in working days when not on stock; null leaves it untouched,
    /// 0 clears it.
    /// </summary>
    public int? OrderFulfillmentDays { get; set; }
    public decimal NetPriceBuy { get; set; }
    public int VatPercentageBuy { get; set; }

    /// <summary>
    /// Kept on stock as a quantity, counted by scanning the EAN code; null leaves it untouched,
    /// so clients that do not know the flag never switch it off.
    /// </summary>
    public bool? WithoutSerialNumbers { get; set; }
}
