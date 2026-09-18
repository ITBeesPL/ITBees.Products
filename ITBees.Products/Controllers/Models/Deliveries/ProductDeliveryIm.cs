namespace ITBees.Products.Controllers.Models.Deliveries;

/// <summary>
/// A received purchase batch: the purchase data shared by the whole batch plus the scanned
/// serialized products - every item gets its own internal serial number (guid) on creation -
/// and the counted pieces of products kept without serial numbers.
/// </summary>
public class ProductDeliveryIm
{
    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SellerNip { get; set; }
    public string? SellerName { get; set; }
    public string? SellerStreet { get; set; }
    public string? SellerPostCode { get; set; }
    public string? SellerCity { get; set; }

    /// <summary>Warranty period counted from the purchase date, in months.</summary>
    public int WarrantyMonths { get; set; }

    /// <summary>Warehouse the products are received into.</summary>
    public Guid WarehouseGuid { get; set; }
    public string? Notes { get; set; }
    public List<ProductDeliveryItemIm> Items { get; set; } = new();

    /// <summary>
    /// Pieces of products kept without serial numbers, one line per product. Nullable on purpose:
    /// MVC would treat a non-nullable list as [Required] and refuse clients that never send it.
    /// </summary>
    public List<ProductDeliveryQuantityItemIm>? QuantityItems { get; set; }
}
