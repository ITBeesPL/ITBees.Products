using ITBees.Products.Entities;

namespace ITBees.Products.Controllers.Models.Deliveries;

/// <summary>
/// A seller remembered from earlier deliveries. Sellers are not an entity of their own - this
/// is the seller data of the most recent delivery bought from them, so the purchase form can
/// suggest it instead of making the operator type it again (a foreign seller has no tax id
/// that could be looked up).
/// </summary>
public class ProductDeliverySellerVm
{
    public ProductDeliverySellerVm()
    {
    }

    /// <param name="latest">The most recently entered delivery bought from the seller.</param>
    /// <param name="deliveriesCount">Number of deliveries bought from the seller so far.</param>
    /// <param name="lastPurchaseDate">The latest purchase date among those deliveries.</param>
    public ProductDeliverySellerVm(ProductDelivery latest, int deliveriesCount, DateTime lastPurchaseDate)
    {
        SellerName = latest.SellerName?.Trim() ?? string.Empty;
        SellerNip = latest.SellerNip;
        SellerStreet = latest.SellerStreet;
        SellerPostCode = latest.SellerPostCode;
        SellerCity = latest.SellerCity;
        DeliveriesCount = deliveriesCount;
        LastPurchaseDate = lastPurchaseDate;
    }

    public string SellerName { get; set; } = string.Empty;
    public string? SellerNip { get; set; }
    public string? SellerStreet { get; set; }
    public string? SellerPostCode { get; set; }
    public string? SellerCity { get; set; }
    public int DeliveriesCount { get; set; }
    public DateTime LastPurchaseDate { get; set; }
}
