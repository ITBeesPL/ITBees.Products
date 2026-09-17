namespace ITBees.Products.Controllers.Models.Deliveries;

/// <summary>One scanned device of a delivery. The order of items is the scanning order.</summary>
public class ProductDeliveryItemIm
{
    public int ProductId { get; set; }

    /// <summary>Serial number given by the manufacturer, as scanned from the device.</summary>
    public string SerialNumber { get; set; } = string.Empty;
}
