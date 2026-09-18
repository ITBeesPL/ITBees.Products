using ITBees.Products.Controllers.Models.Stock;
using ITBees.Products.Entities;

namespace ITBees.Products.Services.Stock;

/// <summary>
/// Stock of the products kept without serial numbers (<see cref="DbModels.Product.WithoutSerialNumbers"/>):
/// quantities per warehouse, summed from <see cref="ProductStockMovement"/> rows. Purchase batches
/// bring such pieces in (<see cref="Deliveries.IProductDeliveryService"/>); documents of the host
/// application - e.g. a package sent to another warehouse - move them on through
/// <see cref="PrepareTransfer"/> and <see cref="Transfer"/>, and undo that with <see cref="Revert"/>.
/// No change may leave a warehouse with less than zero pieces of a product.
/// </summary>
public interface IProductStockService
{
    /// <summary>
    /// How much of each product every warehouse holds: products kept without serial numbers
    /// (summed movements) and serialized ones (their items on stock, counted).
    /// </summary>
    /// <param name="search">Part of the product, producer, EAN or warehouse name.</param>
    /// <param name="withoutSerialNumbers">True: only products counted by quantity; false: only serialized ones; null: both.</param>
    /// <param name="includeEmpty">Also list the pairs whose pieces have all left (quantity 0).</param>
    List<ProductStockLevelVm> GetLevels(string? search, Guid[]? warehouseGuids, int[]? productIds,
        bool? withoutSerialNumbers, bool? includeEmpty);

    /// <summary>
    /// Checks a transfer of pieces before the host stores its document: every product has to be
    /// kept without serial numbers and every source warehouse has to hold the pieces. Lines of
    /// the same product and source warehouse are added up (the order of the first one is kept).
    /// Nothing is written - see <see cref="Transfer"/>.
    /// </summary>
    /// <param name="targetWarehouseGuid">Where the pieces go; null hands them over to a customer.</param>
    ProductStockTransfer PrepareTransfer(IEnumerable<ProductStockTransferRequest>? requests, Guid? targetWarehouseGuid);

    /// <summary>
    /// Moves the pieces as one document of the host - all lines or none. Refused (409) when another
    /// operator took the same pieces in the meantime; nothing stays written then.
    /// </summary>
    /// <param name="documentGuid">The host document - what <see cref="Revert"/> undoes it by.</param>
    /// <param name="documentName">How the operator knows the document, e.g. "Paczka 12".</param>
    void Transfer(ProductStockTransfer transfer, Guid documentGuid, string? documentName);

    /// <summary>
    /// Undoes every movement of a host document and returns what was removed (for
    /// <see cref="Restore"/>). Refused while pieces it brought somewhere are no longer there -
    /// moved on or handed over since. A document without movements is fine: nothing happens.
    /// </summary>
    IReadOnlyList<ProductStockMovement> Revert(Guid documentGuid);

    /// <summary>Writes back movements taken away by <see cref="Revert"/> - when the rest of the host's undo failed.</summary>
    void Restore(IEnumerable<ProductStockMovement> movements);
}
