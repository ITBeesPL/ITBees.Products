namespace ITBees.Products.Services.Labels;

/// <summary>Everything a single warehouse label shows.</summary>
public class StockLabelData
{
    /// <summary>Our internal serial number - the label prints its last characters.</summary>
    public Guid DeviceGuid { get; set; }

    /// <summary>Manufacturer serial number - the label prints its last characters.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    public DateTime? PurchaseDate { get; set; }

    /// <summary>Text encoded in the QR code - see <see cref="ProductsSettings.BuildLabelQrContent"/>.</summary>
    public string QrContent { get; set; } = string.Empty;
}
