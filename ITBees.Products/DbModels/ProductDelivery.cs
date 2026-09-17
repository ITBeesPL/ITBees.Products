namespace ITBees.Products.Entities;

/// <summary>
/// One purchase batch received into a warehouse - the invoice-level data shared by every
/// serialized product scanned in during that delivery (purchase date, invoice, seller, warranty).
/// </summary>
public class ProductDelivery
{
    public Guid Guid { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SellerNip { get; set; }
    public string? SellerName { get; set; }
    public string? SellerStreet { get; set; }
    public string? SellerPostCode { get; set; }
    public string? SellerCity { get; set; }

    /// <summary>Warranty period counted from <see cref="PurchaseDate"/>, in months.</summary>
    public int WarrantyMonths { get; set; }

    /// <summary>Warehouse the batch was received into. Single items may be moved later.</summary>
    public Warehouse? Warehouse { get; set; }
    public Guid? WarehouseGuid { get; set; }
    public string? Notes { get; set; }
    public DateTime Created { get; set; }
    public Guid? CreatedByGuid { get; set; }
    public List<SerializedProductOnStock> Items { get; set; } = new();
}
