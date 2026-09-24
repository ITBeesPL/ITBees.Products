namespace ITBees.Products;

/// <summary>
/// Net / gross arithmetic of the sale price, rounded the way Polish invoices round it: to the
/// grosz, half away from zero (0.005 -> 0.01). Shared by the product validation and by hosts that
/// show gross prices or issue invoices from the net ones.
/// </summary>
public static class ProductPrices
{
    /// <summary>
    /// How far a gross price may drift from the net price with VAT - one grosz. Not every gross price
    /// comes out of a net price in grosze: at 23% 1999.00 does (1625.20 net), 1999.04 does not
    /// (1625.23 gives 1999.03, 1625.24 gives 1999.05), and the tolerance still accepts it. An invoice
    /// counted from the net price then shows the neighbouring amount - a shop that wants the two to
    /// agree should offer only reachable gross prices.
    /// </summary>
    public const decimal GrossTolerance = 0.01m;

    public const int MaxVatPercentage = 100;

    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>Net price with VAT added, rounded to the grosz.</summary>
    public static decimal GrossFromNet(decimal net, int vatPercentage) =>
        Round(net * (100m + vatPercentage) / 100m);

    /// <summary>Net price contained in a gross one, rounded to the grosz.</summary>
    public static decimal NetFromGross(decimal gross, int vatPercentage) =>
        Round(gross * 100m / (100m + vatPercentage));

    /// <summary>True when the gross price is the net price with VAT, give or take <see cref="GrossTolerance"/>.</summary>
    public static bool GrossMatchesNet(decimal gross, decimal net, int vatPercentage) =>
        Math.Abs(gross - GrossFromNet(net, vatPercentage)) <= GrossTolerance;

    /// <summary>
    /// The stored gross price - or, for rows saved before the gross price existed (0 next to a
    /// non-zero net price), the net price with VAT.
    /// </summary>
    public static decimal EffectiveGross(DbModels.Product product) =>
        product.GrossPriceSell != 0m || product.NetPriceSell == 0m
            ? product.GrossPriceSell
            : GrossFromNet(product.NetPriceSell, product.VatPercentageSell);
}
