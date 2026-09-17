using ITBees.Products.Controllers.Models.Stock;
using ITBees.Products.Entities;

namespace ITBees.Products.Controllers.Models.Deliveries;

public class ProductDeliveryVm
{
    public ProductDeliveryVm()
    {
    }

    /// <param name="x">Delivery; its warehouse name is filled in when loaded with it.</param>
    /// <param name="itemsCount">Number of items, for lists that do not load the items.</param>
    /// <param name="items">Items in scanning order, or null when not loaded.</param>
    public ProductDeliveryVm(ProductDelivery x, int itemsCount, List<SerializedProductVm>? items = null)
    {
        Guid = x.Guid;
        PurchaseDate = x.PurchaseDate;
        InvoiceNumber = x.InvoiceNumber;
        SellerNip = x.SellerNip;
        SellerName = x.SellerName;
        SellerStreet = x.SellerStreet;
        SellerPostCode = x.SellerPostCode;
        SellerCity = x.SellerCity;
        WarrantyMonths = x.WarrantyMonths;
        WarrantyUntil = x.PurchaseDate.AddMonths(x.WarrantyMonths);
        WarehouseGuid = x.WarehouseGuid;
        WarehouseName = x.Warehouse?.WarehouseName;
        Notes = x.Notes;
        Created = x.Created;
        CreatedByGuid = x.CreatedByGuid;
        ItemsCount = itemsCount;
        Items = items;
    }

    public Guid Guid { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SellerNip { get; set; }
    public string? SellerName { get; set; }
    public string? SellerStreet { get; set; }
    public string? SellerPostCode { get; set; }
    public string? SellerCity { get; set; }
    public int WarrantyMonths { get; set; }
    public DateTime WarrantyUntil { get; set; }
    public Guid? WarehouseGuid { get; set; }
    public string? WarehouseName { get; set; }
    public string? Notes { get; set; }
    public DateTime Created { get; set; }
    public Guid? CreatedByGuid { get; set; }
    public int ItemsCount { get; set; }

    /// <summary>Items in scanning order; null in list results, which carry only the count.</summary>
    public List<SerializedProductVm>? Items { get; set; }
}
