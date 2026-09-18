using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.Warehouses;
using ITBees.Products.Entities;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.UserManager.Interfaces;

namespace ITBees.Products.Services.Warehouses;

public class WarehouseService : IWarehouseService
{
    private const int MaxNameLength = 200;

    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IReadOnlyRepository<Warehouse> _warehouseRoRepo;
    private readonly IWriteOnlyRepository<Warehouse> _warehouseWoRepo;
    private readonly IReadOnlyRepository<SerializedProductOnStock> _stockRoRepo;
    private readonly IReadOnlyRepository<ProductDelivery> _deliveryRoRepo;
    private readonly IReadOnlyRepository<ProductStockMovement> _movementRoRepo;

    public WarehouseService(
        IAspCurrentUserService aspCurrentUserService,
        IReadOnlyRepository<Warehouse> warehouseRoRepo,
        IWriteOnlyRepository<Warehouse> warehouseWoRepo,
        IReadOnlyRepository<SerializedProductOnStock> stockRoRepo,
        IReadOnlyRepository<ProductDelivery> deliveryRoRepo,
        IReadOnlyRepository<ProductStockMovement> movementRoRepo)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _warehouseRoRepo = warehouseRoRepo;
        _warehouseWoRepo = warehouseWoRepo;
        _stockRoRepo = stockRoRepo;
        _deliveryRoRepo = deliveryRoRepo;
        _movementRoRepo = movementRoRepo;
    }

    public WarehouseVm Get(Guid guid)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        return new WarehouseVm(GetOrThrow(guid), CountItemsOnStock(guid), CountUnitsOnStock(guid));
    }

    public List<WarehouseVm> GetAll(bool? isActive)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        return _warehouseRoRepo
            .GetData(x => isActive == null || x.IsActive == isActive)
            .OrderBy(x => x.WarehouseName)
            // A handful of warehouses at most - count queries per row are fine here.
            .Select(x => new WarehouseVm(x, CountItemsOnStock(x.Guid), CountUnitsOnStock(x.Guid)))
            .ToList();
    }

    public WarehouseVm Create(WarehouseIm warehouseIm)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var name = ValidateName(warehouseIm?.WarehouseName, null);

        var warehouse = _warehouseWoRepo.InsertData(new Warehouse
        {
            Guid = Guid.NewGuid(),
            WarehouseName = name,
            IsActive = warehouseIm!.IsActive
        });

        return new WarehouseVm(warehouse);
    }

    public WarehouseVm Update(WarehouseUm warehouseUm)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        if (warehouseUm == null)
        {
            throw new FasApiErrorException("Brak danych magazynu.", 400);
        }

        GetOrThrow(warehouseUm.Guid);
        var name = ValidateName(warehouseUm.WarehouseName, warehouseUm.Guid);

        var updated = _warehouseWoRepo.UpdateData(x => x.Guid == warehouseUm.Guid, x =>
        {
            x.WarehouseName = name;
            x.IsActive = warehouseUm.IsActive;
        }).First();

        return new WarehouseVm(updated, CountItemsOnStock(updated.Guid), CountUnitsOnStock(updated.Guid));
    }

    public void Delete(Guid guid)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var warehouse = GetOrThrow(guid);

        // Anything that ever pointed at the warehouse keeps it alive - history must stay readable.
        var isUsed = _stockRoRepo.HasData(x => x.WarehouseGuid == guid) ||
                     _deliveryRoRepo.HasData(x => x.WarehouseGuid == guid) ||
                     _movementRoRepo.HasData(x => x.WarehouseGuid == guid);
        if (isUsed)
        {
            throw new FasApiErrorException(
                $"Magazyn „{warehouse.WarehouseName}” był już używany (urządzenia, produkty lub dostawy) - " +
                "nie można go usunąć. Możesz go dezaktywować.", 400);
        }

        _warehouseWoRepo.DeleteData(x => x.Guid == guid);
    }

    private Warehouse GetOrThrow(Guid guid)
    {
        var warehouse = _warehouseRoRepo.GetData(x => x.Guid == guid).FirstOrDefault();
        if (warehouse == null)
        {
            throw new FasApiErrorException("Nie znaleziono magazynu.", 404);
        }

        return warehouse;
    }

    private int CountItemsOnStock(Guid warehouseGuid)
    {
        return _stockRoRepo.GetDataCount(x => x.WarehouseGuid == warehouseGuid && !x.DeliveredToEndCustomer);
    }

    /// <summary>Pieces of products kept without serial numbers, all products together.</summary>
    private int CountUnitsOnStock(Guid warehouseGuid)
    {
        // Summed as nullable: SUM over no rows is NULL in SQL.
        return _movementRoRepo.GetDataQueryable(x => x.WarehouseGuid == warehouseGuid)
            .Select(x => (int?)x.Quantity)
            .Sum() ?? 0;
    }

    private string ValidateName(string? warehouseName, Guid? ownGuid)
    {
        var name = (warehouseName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new FasApiErrorException("Podaj nazwę magazynu.", 400);
        }

        if (name.Length > MaxNameLength)
        {
            throw new FasApiErrorException($"Nazwa magazynu może mieć najwyżej {MaxNameLength} znaków.", 400);
        }

        if (_warehouseRoRepo.HasData(x => x.WarehouseName == name && x.Guid != ownGuid))
        {
            throw new FasApiErrorException($"Magazyn o nazwie „{name}” już istnieje.", 400);
        }

        return name;
    }
}
