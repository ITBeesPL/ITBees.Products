namespace ITBees.Products.Entities;

/// <summary>
/// A trial product (<see cref="ProductTrial"/>) taken by a customer. One row per product and customer -
/// the unique index keeps a customer from taking the same trial twice, also with two requests at once.
/// </summary>
public class ProductTrialUse
{
    public int Id { get; set; }

    public DbModels.Product? Product { get; set; }
    public int ProductId { get; set; }

    /// <summary>
    /// Who took the trial, the way the host counts its customers - a company, a school, a user account.
    /// The library stores only the guid.
    /// </summary>
    public Guid CustomerGuid { get; set; }

    public DateTime UsedAtUtc { get; set; }

    /// <summary>The user who took the trial on the customer's behalf; null when the host does not say.</summary>
    public Guid? UsedByGuid { get; set; }
}
