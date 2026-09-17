using ITBees.Products.DbModels;

namespace ITBees.Products.Entities;

public class SerializedProductOnStock
{
    public int Id { get; set; }

    /// <summary>
    /// Our own serial number, assigned when the item is received - printed (as a QR code) on
    /// the warehouse label. Null only for rows created before internal numbering existed.
    /// </summary>
    public Guid? Guid { get; set; }

    /// <summary>Serial number given by the manufacturer, scanned from the device.</summary>
    public string SerialNumber { get; set; }
    public Warehouse Warehouse { get; set; }
    public Guid? WarehouseGuid { get; set; }
    public Product Product { get; set; }
    public int ProductId { get; set; }
    public DateTime Received { get; set; }
    public bool DeliveredToEndCustomer { get; set; }

    /// <summary>Purchase batch the item arrived in (invoice, seller, warranty).</summary>
    public ProductDelivery? ProductDelivery { get; set; }
    public Guid? ProductDeliveryGuid { get; set; }

    /// <summary>1-based scanning order inside the delivery - labels are printed in this order.</summary>
    public int PositionInDelivery { get; set; }
}
