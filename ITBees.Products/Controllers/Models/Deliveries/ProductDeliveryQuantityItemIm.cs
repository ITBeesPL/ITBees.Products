namespace ITBees.Products.Controllers.Models.Deliveries;

/// <summary>
/// Pieces of a product kept without serial numbers received in a delivery - counted by scanning
/// the EAN code on each of them. The order of these lines is the scanning order.
/// </summary>
public class ProductDeliveryQuantityItemIm
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
