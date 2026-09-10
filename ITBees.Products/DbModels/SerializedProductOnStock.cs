using ITBees.Products.DbModels;

namespace ITBees.Products.Entities;

public class SerializedProductOnStock
{
    public int Id { get; set; }
    public string SerialNumber { get; set; }
    public Warehouse Warehouse { get; set; }
    public Guid? WarehouseGuid { get; set; }
    public Product Product { get; set; }
    public int ProductId { get; set; }
    public DateTime Received { get; set; }
    public bool DeliveredToEndCustomer { get; set; }
}