using ITBees.Products.Entities;

namespace ITBees.Products;

/// <summary>
/// Validity period of a service (<see cref="DbModels.Product.ValidityPeriod"/>): its limits, the end
/// date of a purchase and the Polish wording ("12 miesięcy") hosts put on orders and screens.
/// </summary>
public static class ProductValidity
{
    public const int MaxDays = 3660;
    public const int MaxMonths = 120;
    public const int MaxYears = 10;

    public static int MaxPeriod(ProductValidityUnit unit) => unit switch
    {
        ProductValidityUnit.Day => MaxDays,
        ProductValidityUnit.Month => MaxMonths,
        ProductValidityUnit.Year => MaxYears,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };

    /// <summary>
    /// When a service bought (or extended) at <paramref name="from"/> stops being valid. A month or a
    /// year ends on the same day of the month - 31 January plus one month is 28/29 February.
    /// </summary>
    public static DateTime ValidTo(DateTime from, int period, ProductValidityUnit unit) => unit switch
    {
        ProductValidityUnit.Day => from.AddDays(period),
        ProductValidityUnit.Month => from.AddMonths(period),
        ProductValidityUnit.Year => from.AddYears(period),
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };

    /// <summary>End of the service bought at <paramref name="from"/>; null when it never ends (or is goods).</summary>
    public static DateTime? ValidTo(DbModels.Product product, DateTime from) =>
        product.ValidityPeriod is > 0 && product.ValidityUnit != null
            ? ValidTo(from, product.ValidityPeriod.Value, product.ValidityUnit.Value)
            : null;

    /// <summary>"1 dzień", "30 dni", "1 miesiąc", "3 miesiące", "12 miesięcy", "1 rok", "2 lata", "5 lat".</summary>
    public static string Describe(int period, ProductValidityUnit unit)
    {
        var word = unit switch
        {
            ProductValidityUnit.Day => period == 1 ? "dzień" : "dni",
            ProductValidityUnit.Month => PolishPlural(period, "miesiąc", "miesiące", "miesięcy"),
            ProductValidityUnit.Year => PolishPlural(period, "rok", "lata", "lat"),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
        };
        return $"{period} {word}";
    }

    /// <summary>The validity of a product in words, or null when it has none.</summary>
    public static string? Describe(DbModels.Product product) =>
        product.ValidityPeriod is > 0 && product.ValidityUnit != null
            ? Describe(product.ValidityPeriod.Value, product.ValidityUnit.Value)
            : null;

    private static string PolishPlural(int n, string one, string few, string many)
    {
        if (n == 1)
        {
            return one;
        }

        var lastDigit = n % 10;
        var lastTwoDigits = n % 100;
        return lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14 ? few : many;
    }
}
