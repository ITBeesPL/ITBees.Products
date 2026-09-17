namespace ITBees.Products.Controllers.Models.Warehouses;

public class WarehouseUm
{
    public Guid Guid { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
