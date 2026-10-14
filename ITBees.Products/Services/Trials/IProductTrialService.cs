namespace ITBees.Products.Services.Trials;

/// <summary>
/// Who has taken which trial product (<see cref="ProductTrial"/>). A customer may take each trial once:
/// the host calls <see cref="TryClaim"/> before it activates the trial and uses
/// <see cref="GetUsedTrialProductIds"/> to hide or disable the trials already taken. A customer is whatever
/// the host sells to (a company, a school, a user account) - only its guid is stored.
/// </summary>
public interface IProductTrialService
{
    /// <summary>Ids of the trial products the customer has already taken.</summary>
    IReadOnlySet<int> GetUsedTrialProductIds(Guid customerGuid);

    bool HasUsedTrial(int productId, Guid customerGuid);

    /// <summary>
    /// Records that the customer takes the trial. False when the customer has taken it already - also when
    /// another request took it at the same moment. 404 for a missing product, 400 for a product with a price.
    /// </summary>
    bool TryClaim(int productId, Guid customerGuid, Guid? usedByGuid);

    /// <summary>
    /// Gives a claimed trial back, so the customer may take it again - for a host whose activation of the
    /// trial failed after <see cref="TryClaim"/>, or for an operator granting a trial once more.
    /// </summary>
    void Release(int productId, Guid customerGuid);
}
