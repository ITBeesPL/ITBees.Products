namespace ITBees.Products.Controllers.Models.Stock;

public class SerializedProductUm
{
    /// <summary>Internal serial number (guid) of the item being updated.</summary>
    public Guid Guid { get; set; }

    /// <summary>Corrected manufacturer serial number.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Warehouse the item is kept in - change it to move the item.</summary>
    public Guid? WarehouseGuid { get; set; }
    public bool DeliveredToEndCustomer { get; set; }
}
