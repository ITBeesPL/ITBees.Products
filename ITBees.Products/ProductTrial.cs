namespace ITBees.Products;

/// <summary>
/// Trial products ("wersja próbna"): a product that costs nothing is a trial. A host gives it away
/// without a payment - no payment operator is involved - and lets each customer take it only once;
/// <see cref="Services.Trials.IProductTrialService"/> keeps track of who has taken which trial.
/// </summary>
public static class ProductTrial
{
    /// <summary>True when the sale price is zero (see <see cref="ProductPrices.EffectiveGross"/>).</summary>
    public static bool IsTrial(DbModels.Product product) => ProductPrices.EffectiveGross(product) <= 0m;
}
