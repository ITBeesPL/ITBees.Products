namespace ITBees.Products.Controllers.Models.Stock;

/// <summary>
/// How much of a product one warehouse holds. For a product kept without serial numbers it is
/// the sum of its movements; for a serialized one, the number of its items on stock there.
/// </summary>
public class ProductStockLevelVm
{
    public ProductStockLevelVm()
    {
    }

    public ProductStockLevelVm(DbModels.Product? product, int productId, Guid? warehouseGuid, string? warehouseName,
        int quantity, DateTime? lastChange)
    {
        ProductId = productId;
        ProductName = product?.ShortDescription;
        ProducerName = product?.Producer?.Name;
        Ean = string.IsNullOrWhiteSpace(product?.Ean) ? null : product.Ean;
        WithoutSerialNumbers = product?.WithoutSerialNumbers ?? false;
        WarehouseGuid = warehouseGuid;
        WarehouseName = warehouseName;
        Quantity = quantity;
        LastChange = lastChange;
    }

    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProducerName { get; set; }
    public string? Ean { get; set; }

    /// <summary>Counted by quantity (true) or item by item, by serial number (false).</summary>
    public bool WithoutSerialNumbers { get; set; }

    /// <summary>Null for serialized items kept outside of every warehouse.</summary>
    public Guid? WarehouseGuid { get; set; }

    public string? WarehouseName { get; set; }
    public int Quantity { get; set; }

    /// <summary>
    /// Last change of the quantity (the last movement); for serialized items the last time one
    /// of them was received.
    /// </summary>
    public DateTime? LastChange { get; set; }
}
