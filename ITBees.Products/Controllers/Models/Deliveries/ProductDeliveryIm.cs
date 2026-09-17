namespace ITBees.Products.Controllers.Models.Deliveries;

/// <summary>
/// A received purchase batch: the purchase data shared by the whole batch plus the scanned
/// serialized products. Every item gets its own internal serial number (guid) on creation.
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
}
