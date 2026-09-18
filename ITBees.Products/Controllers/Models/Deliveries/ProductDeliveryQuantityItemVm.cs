using ITBees.Products.Entities;

namespace ITBees.Products.Controllers.Models.Deliveries;

/// <summary>Pieces of a product kept without serial numbers that arrived in a delivery.</summary>
public class ProductDeliveryQuantityItemVm
{
    public ProductDeliveryQuantityItemVm()
    {
    }

    /// <param name="x">The delivery movement; product names are filled in when loaded with it.</param>
    public ProductDeliveryQuantityItemVm(ProductStockMovement x)
    {
        ProductId = x.ProductId;
        ProductName = x.Product?.ShortDescription;
        ProducerName = x.Product?.Producer?.Name;
        Ean = string.IsNullOrWhiteSpace(x.Product?.Ean) ? null : x.Product.Ean;
        Quantity = x.Quantity;
        Position = x.Position;
    }

    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProducerName { get; set; }
    public string? Ean { get; set; }
    public int Quantity { get; set; }

    /// <summary>1-based scanning order of the product inside the delivery.</summary>
    public int Position { get; set; }
}
