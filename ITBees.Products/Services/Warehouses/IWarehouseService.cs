using ITBees.Products.Controllers.Models.Warehouses;

namespace ITBees.Products.Services.Warehouses;

public interface IWarehouseService
{
    WarehouseVm Get(Guid guid);

    /// <param name="isActive">Null returns every warehouse, otherwise only the (in)active ones.</param>
    List<WarehouseVm> GetAll(bool? isActive);
    WarehouseVm Create(WarehouseIm warehouseIm);
    WarehouseVm Update(WarehouseUm warehouseUm);

    /// <summary>Deletes an empty warehouse; one that was ever used can only be deactivated.</summary>
    void Delete(Guid guid);
}
