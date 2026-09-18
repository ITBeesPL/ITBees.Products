using ITBees.Products.Services.Labels;

namespace ITBees.Products;

/// <summary>
/// Host-provided settings of the products / warehouse module - pass them to
/// <see cref="Setup.ProductsSetup.Register"/>.
/// </summary>
public class ProductsSettings
{
    public const string GuidPlaceholder = "{guid}";

    /// <summary>
    /// Address of the page showing the details of a single stocked device, e.g.
    /// "https://admin.example.com/warehouse/device". The device guid is appended as the last
    /// path segment, or substituted for a "{guid}" placeholder when the address contains one
    /// (e.g. "https://admin.example.com/device?id={guid}"). The resulting link is what the QR
    /// code on a warehouse label points to. When empty, the QR code carries the bare guid.
    /// </summary>
    public string? DeviceWarehouseUrl { get; set; }

    /// <summary>
    /// Prints the purchase date on warehouse labels, as a third line under the internal id and
    /// the serial number. Off by default - the label then shows those two only; the date stays
    /// available in the device details the label's QR code leads to.
    /// </summary>
    public bool PrintPurchaseDateOnLabels { get; set; }

    /// <summary>
    /// Owner of the stocked devices printed at the top of every warehouse label, e.g.
    /// "Octopark spółka z o.o." - broken into two lines when it has more than one word (three for a
    /// long name), the first line bold. Polish letters are printed as such. Null or empty prints
    /// no name.
    /// </summary>
    public string? LabelCompanyName { get; set; }

    /// <summary>
    /// Small pictogram printed next to <see cref="LabelCompanyName"/> - load it once at startup,
    /// e.g. with <see cref="StockLabelLogo.FromEmbeddedResource"/>, so a broken drawing stops the
    /// application at once instead of the first label print. Null prints no pictogram.
    /// </summary>
    public StockLabelLogo? LabelLogo { get; set; }

    /// <summary>Link to the device details page, or null when no address is configured.</summary>
    public string? BuildDeviceUrl(Guid deviceGuid)
    {
        var url = DeviceWarehouseUrl?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        return url.Contains(GuidPlaceholder, StringComparison.OrdinalIgnoreCase)
            ? url.Replace(GuidPlaceholder, deviceGuid.ToString(), StringComparison.OrdinalIgnoreCase)
            : $"{url.TrimEnd('/')}/{deviceGuid}";
    }

    /// <summary>What gets encoded in the label QR code: the details link, or the guid alone.</summary>
    public string BuildLabelQrContent(Guid deviceGuid)
    {
        // An upper-case guid fits the QR alphanumeric mode, which keeps the code small.
        return BuildDeviceUrl(deviceGuid) ?? deviceGuid.ToString().ToUpperInvariant();
    }
}
