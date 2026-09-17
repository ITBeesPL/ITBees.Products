using ITBees.Products.Entities;

namespace ITBees.Products.Controllers.Models.Warehouses;

public class WarehouseVm
{
    public WarehouseVm()
    {
    }

    public WarehouseVm(Warehouse x, int itemsOnStockCount = 0)
    {
        Guid = x.Guid;
        WarehouseName = x.WarehouseName;
        IsActive = x.IsActive;
        ItemsOnStockCount = itemsOnStockCount;
    }

    public Guid Guid { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>Serialized products currently kept in the warehouse (not yet handed over to a customer).</summary>
    public int ItemsOnStockCount { get; set; }
}
