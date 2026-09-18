namespace ITBees.Products.Services.Stock;

/// <summary>What the host asks to move: pieces of one product, taken out of one warehouse.</summary>
public class ProductStockTransferRequest
{
    public int ProductId { get; set; }

    /// <summary>Warehouse the pieces are taken out of.</summary>
    public Guid? SourceWarehouseGuid { get; set; }

    public int Quantity { get; set; }
}

/// <summary>One checked line of a <see cref="ProductStockTransfer"/>, with the names of what it moves.</summary>
public class ProductStockTransferLine
{
    /// <summary>1-based position - the order the pieces were scanned in.</summary>
    public int Position { get; init; }

    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? ProducerName { get; init; }
    public string? Ean { get; init; }
    public Guid SourceWarehouseGuid { get; init; }
    public string SourceWarehouseName { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

/// <summary>
/// Pieces of products kept without serial numbers that a document of the host moves: to
/// <see cref="TargetWarehouseGuid"/>, or out of the stock to a customer when it is null. Made by
/// <see cref="IProductStockService.PrepareTransfer"/> - which checks the stock - and carried out
/// by <see cref="IProductStockService.Transfer"/>.
/// </summary>
public class ProductStockTransfer
{
    internal ProductStockTransfer(Guid? targetWarehouseGuid, List<ProductStockTransferLine> lines)
    {
        TargetWarehouseGuid = targetWarehouseGuid;
        Lines = lines;
    }

    public Guid? TargetWarehouseGuid { get; }
    public IReadOnlyList<ProductStockTransferLine> Lines { get; }
    public int TotalQuantity => Lines.Sum(x => x.Quantity);
}
