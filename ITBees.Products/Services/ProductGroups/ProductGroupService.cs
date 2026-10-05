using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.ProductGroups;
using ITBees.Products.Entities;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.UserManager.Interfaces;

namespace ITBees.Products.Services.ProductGroups;

public class ProductGroupService : IProductGroupService
{
    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IReadOnlyRepository<ProductGroup> _groupRoRepo;
    private readonly IWriteOnlyRepository<ProductGroup> _groupWoRepo;
    private readonly IReadOnlyRepository<ProductInGroup> _membershipRoRepo;

    public ProductGroupService(
        IAspCurrentUserService aspCurrentUserService,
        IReadOnlyRepository<ProductGroup> groupRoRepo,
        IWriteOnlyRepository<ProductGroup> groupWoRepo,
        IReadOnlyRepository<ProductInGroup> membershipRoRepo)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _groupRoRepo = groupRoRepo;
        _groupWoRepo = groupWoRepo;
        _membershipRoRepo = membershipRoRepo;
    }

    public ProductGroupVm Get(int productGroupId)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var group = Find(productGroupId);
        return new ProductGroupVm(group, _membershipRoRepo.GetDataCount(x => x.ProductGroupId == group.Id));
    }

    public List<ProductGroupVm> GetAll()
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var counts = _membershipRoRepo.GetDataQueryable(x => true)
            .GroupBy(x => x.ProductGroupId)
            .Select(x => new { ProductGroupId = x.Key, Count = x.Count() })
            .ToList()
            .ToDictionary(x => x.ProductGroupId, x => x.Count);

        return _groupRoRepo.GetData(x => true)
            .OrderBy(x => x.Name)
            .Select(x => new ProductGroupVm(x, counts.GetValueOrDefault(x.Id)))
            .ToList();
    }

    public ProductGroupVm Create(ProductGroupIm productGroupIm)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var name = ValidateName(productGroupIm?.Name, null);
        var group = _groupWoRepo.InsertData(new ProductGroup
        {
            Name = name,
            Description = ValidateDescription(productGroupIm!.Description),
            Created = DateTime.UtcNow,
            CreatedByGuid = _aspCurrentUserService.GetCurrentUserGuid()
        });

        return new ProductGroupVm(group, 0);
    }

    public ProductGroupVm Update(ProductGroupUm productGroupUm)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        if (productGroupUm == null)
        {
            throw new FasApiErrorException("Brak danych grupy.", 400);
        }

        var group = Find(productGroupUm.Id);
        var name = ValidateName(productGroupUm.Name, group.Id);
        var description = ValidateDescription(productGroupUm.Description);
        var updated = _groupWoRepo.UpdateData(x => x.Id == group.Id, x =>
        {
            x.Name = name;
            x.Description = description;
        }).First();

        return new ProductGroupVm(updated, _membershipRoRepo.GetDataCount(x => x.ProductGroupId == group.Id));
    }

    public void Delete(int productGroupId)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var group = Find(productGroupId);
        // The memberships go along by the database cascade; the products stay in the catalogue.
        _groupWoRepo.DeleteData(x => x.Id == group.Id);
    }

    private ProductGroup Find(int productGroupId)
    {
        return _groupRoRepo.GetData(x => x.Id == productGroupId).FirstOrDefault()
               ?? throw new FasApiErrorException("Nie znaleziono grupy produktów.", 404);
    }

    /// <summary>A trimmed, non-empty name no other group has (letter case aside).</summary>
    private string ValidateName(string? name, int? ownId)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new FasApiErrorException("Podaj nazwę grupy.", 400);
        }

        if (trimmed.Length > ProductGroupLimits.MaxNameLength)
        {
            throw new FasApiErrorException(
                $"Nazwa grupy może mieć najwyżej {ProductGroupLimits.MaxNameLength} znaków.", 400);
        }

        // Compared in memory too: the database collation decides the unique index, but a group
        // differing only in letter case would be confusing anyway.
        var lower = trimmed.ToLower();
        if (_groupRoRepo.HasData(x => x.Id != ownId && x.Name.ToLower() == lower))
        {
            throw new FasApiErrorException($"Grupa „{trimmed}” już istnieje.", 400);
        }

        return trimmed;
    }

    private static string? ValidateDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > ProductGroupLimits.MaxDescriptionLength)
        {
            throw new FasApiErrorException(
                $"Opis grupy może mieć najwyżej {ProductGroupLimits.MaxDescriptionLength} znaków.", 400);
        }

        return trimmed;
    }
}
