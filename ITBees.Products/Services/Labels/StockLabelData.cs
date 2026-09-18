namespace ITBees.Products.Services.Labels;

/// <summary>Everything a single warehouse label shows.</summary>
public class StockLabelData
{
    /// <summary>Our internal serial number - the label prints its last characters.</summary>
    public Guid DeviceGuid { get; set; }

    /// <summary>Manufacturer serial number - the label prints its last characters.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Printed as the last line; null leaves the line out. <see cref="StockLabelService"/> fills
    /// it only when <see cref="ProductsSettings.PrintPurchaseDateOnLabels"/> is on.
    /// </summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>Text encoded in the QR code - see <see cref="ProductsSettings.BuildLabelQrContent"/>.</summary>
    public string QrContent { get; set; } = string.Empty;

    /// <summary>
    /// Owner of the device, printed at the top of the text column (two or three lines, Polish
    /// letters allowed); null or empty leaves it out. See <see cref="ProductsSettings.LabelCompanyName"/>.
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Pictogram printed next to the company name; null leaves it out. See
    /// <see cref="ProductsSettings.LabelLogo"/>.
    /// </summary>
    public StockLabelLogo? Logo { get; set; }
}
