using ITBees.Products.Entities;
using ITBees.Products.Services.Labels;

namespace ITBees.Products.Controllers.Models.Stock;

/// <summary>
/// A single serialized product kept on stock, together with the purchase data of the delivery
/// it arrived in. Related data is filled in only when the entity was loaded with it.
/// </summary>
public class SerializedProductVm
{
    public SerializedProductVm()
    {
    }

    public SerializedProductVm(SerializedProductOnStock x, ProductsSettings? settings = null)
    {
        Id = x.Id;
        Guid = x.Guid;
        LabelCode = x.Guid.HasValue ? StockLabelFormat.InternalCode(x.Guid.Value) : null;
        DetailsUrl = x.Guid.HasValue ? settings?.BuildDeviceUrl(x.Guid.Value) : null;
        SerialNumber = x.SerialNumber;
        ProductId = x.ProductId;
        ProductName = x.Product?.ShortDescription;
        ProducerName = x.Product?.Producer?.Name;
        WarehouseGuid = x.WarehouseGuid;
        WarehouseName = x.Warehouse?.WarehouseName;
        Received = x.Received;
        DeliveredToEndCustomer = x.DeliveredToEndCustomer;
        ProductDeliveryGuid = x.ProductDeliveryGuid;
        PositionInDelivery = x.PositionInDelivery;

        var delivery = x.ProductDelivery;
        if (delivery != null)
        {
            PurchaseDate = delivery.PurchaseDate;
            InvoiceNumber = delivery.InvoiceNumber;
            SellerName = delivery.SellerName;
            SellerNip = delivery.SellerNip;
            WarrantyMonths = delivery.WarrantyMonths;
            WarrantyUntil = delivery.PurchaseDate.AddMonths(delivery.WarrantyMonths);
        }
    }

    public int Id { get; set; }

    /// <summary>Our internal serial number - the value behind the label QR code.</summary>
    public Guid? Guid { get; set; }

    /// <summary>Short code printed on the label (last characters of <see cref="Guid"/>).</summary>
    public string? LabelCode { get; set; }

    /// <summary>Link encoded in the label QR code; null when no DeviceWarehouseUrl is configured.</summary>
    public string? DetailsUrl { get; set; }

    /// <summary>Serial number given by the manufacturer.</summary>
    public string SerialNumber { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProducerName { get; set; }
    public Guid? WarehouseGuid { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime Received { get; set; }
    public bool DeliveredToEndCustomer { get; set; }
    public Guid? ProductDeliveryGuid { get; set; }
    public int PositionInDelivery { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SellerName { get; set; }
    public string? SellerNip { get; set; }
    public int? WarrantyMonths { get; set; }
    public DateTime? WarrantyUntil { get; set; }
}
