namespace ITBees.Products.Entities;

/// <summary>
/// One change of the stock of a product kept without serial numbers
/// (<see cref="DbModels.Product.WithoutSerialNumbers"/>). Such pieces are not told apart, so they
/// have no rows of their own: the quantity of a product in a warehouse is the sum of its movements
/// there. A movement is stored together with the document that caused it (a purchase batch, or a
/// document of the host application such as a package) and is removed only together with it.
/// </summary>
public class ProductStockMovement
{
    public int Id { get; set; }

    public DbModels.Product? Product { get; set; }
    public int ProductId { get; set; }

    public Warehouse? Warehouse { get; set; }
    public Guid WarehouseGuid { get; set; }

    /// <summary>Pieces that came into the warehouse (positive) or left it (negative).</summary>
    public int Quantity { get; set; }

    public ProductStockMovementType Type { get; set; }

    /// <summary>The purchase batch the pieces arrived in - set for <see cref="ProductStockMovementType.Delivery"/>.</summary>
    public ProductDelivery? ProductDelivery { get; set; }
    public Guid? ProductDeliveryGuid { get; set; }

    /// <summary>
    /// Document of the host application that moved the pieces, e.g. a package sent to another
    /// warehouse. All movements of one document are recorded and reverted together.
    /// </summary>
    public Guid? DocumentGuid { get; set; }

    /// <summary>What the operator knows that document as, e.g. "Paczka 12".</summary>
    public string? DocumentName { get; set; }

    /// <summary>1-based position inside the document - the order the pieces were scanned in.</summary>
    public int Position { get; set; }

    public DateTime Created { get; set; }
    public Guid? CreatedByGuid { get; set; }
}
