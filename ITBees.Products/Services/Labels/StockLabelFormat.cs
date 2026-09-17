namespace ITBees.Products.Services.Labels;

/// <summary>The short, human-readable codes printed on a warehouse label.</summary>
public static class StockLabelFormat
{
    public const int InternalCodeLength = 6;
    public const int SerialNumberSuffixLength = 4;

    /// <summary>Last characters of our internal serial number (guid), upper-case.</summary>
    public static string InternalCode(Guid deviceGuid)
    {
        return deviceGuid.ToString("N")[^InternalCodeLength..].ToUpperInvariant();
    }

    /// <summary>Last characters of the manufacturer serial number.</summary>
    public static string SerialNumberSuffix(string? serialNumber)
    {
        var serial = (serialNumber ?? string.Empty).Trim();
        return serial.Length <= SerialNumberSuffixLength ? serial : serial[^SerialNumberSuffixLength..];
    }
}
