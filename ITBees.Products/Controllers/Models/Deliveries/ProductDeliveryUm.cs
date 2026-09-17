namespace ITBees.Products.Controllers.Models.Deliveries;

/// <summary>Corrects the purchase data of a delivery. Its items are edited one by one.</summary>
public class ProductDeliveryUm
{
    public Guid Guid { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SellerNip { get; set; }
    public string? SellerName { get; set; }
    public string? SellerStreet { get; set; }
    public string? SellerPostCode { get; set; }
    public string? SellerCity { get; set; }
    public int WarrantyMonths { get; set; }
    public string? Notes { get; set; }
}
