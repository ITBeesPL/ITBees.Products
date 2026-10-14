using ITBees.Interfaces.Repository;
using ITBees.Products.Entities;
using ITBees.RestfulApiControllers.Exceptions;

namespace ITBees.Products.Services.Trials;

public class ProductTrialService : IProductTrialService
{
    private readonly IReadOnlyRepository<DbModels.Product> _productRoRepo;
    private readonly IReadOnlyRepository<ProductTrialUse> _trialUseRoRepo;
    private readonly IWriteOnlyRepository<ProductTrialUse> _trialUseWoRepo;

    public ProductTrialService(
        IReadOnlyRepository<DbModels.Product> productRoRepo,
        IReadOnlyRepository<ProductTrialUse> trialUseRoRepo,
        IWriteOnlyRepository<ProductTrialUse> trialUseWoRepo)
    {
        _productRoRepo = productRoRepo;
        _trialUseRoRepo = trialUseRoRepo;
        _trialUseWoRepo = trialUseWoRepo;
    }

    public IReadOnlySet<int> GetUsedTrialProductIds(Guid customerGuid)
    {
        return _trialUseRoRepo.GetData(x => x.CustomerGuid == customerGuid)
            .Select(x => x.ProductId)
            .ToHashSet();
    }

    public bool HasUsedTrial(int productId, Guid customerGuid)
    {
        return _trialUseRoRepo.HasData(x => x.ProductId == productId && x.CustomerGuid == customerGuid);
    }

    public bool TryClaim(int productId, Guid customerGuid, Guid? usedByGuid)
    {
        var product = _productRoRepo.GetData(x => x.Id == productId).FirstOrDefault()
                      ?? throw new FasApiErrorException("Nie znaleziono produktu.", 404);
        if (!ProductTrial.IsTrial(product))
        {
            throw new FasApiErrorException("Ten produkt ma cenę - nie jest wersją próbną.", 400);
        }

        if (HasUsedTrial(productId, customerGuid))
        {
            return false;
        }

        try
        {
            _trialUseWoRepo.InsertData(new ProductTrialUse
            {
                ProductId = productId,
                CustomerGuid = customerGuid,
                UsedAtUtc = DateTime.UtcNow,
                UsedByGuid = usedByGuid
            });
            return true;
        }
        // A request running at the same moment took the trial first - the unique index refused this row.
        catch (Exception) when (HasUsedTrial(productId, customerGuid))
        {
            return false;
        }
    }

    public void Release(int productId, Guid customerGuid)
    {
        _trialUseWoRepo.DeleteData(x => x.ProductId == productId && x.CustomerGuid == customerGuid);
    }
}
