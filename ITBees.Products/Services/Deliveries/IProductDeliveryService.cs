using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.Deliveries;

namespace ITBees.Products.Services.Deliveries;

public interface IProductDeliveryService
{
    /// <summary>Delivery with its items, in scanning order.</summary>
    ProductDeliveryVm Get(Guid guid);

    PaginatedResult<ProductDeliveryVm> GetPaginated(string? search, Guid[]? warehouseGuids, int? page,
        int? pageSize, string? sortColumn, SortOrder? sortOrder);

    /// <summary>
    /// Sellers of the earlier deliveries, for suggestions in the purchase form: one entry per
    /// seller (name + tax id), carrying the seller data of the most recently entered delivery.
    /// </summary>
    /// <param name="search">Part of the seller name or tax id; empty returns the most recently used sellers.</param>
    /// <param name="limit">Maximum number of suggestions.</param>
    List<ProductDeliverySellerVm> GetSellers(string? search, int? limit);

    /// <summary>
    /// Receives a purchase batch: stores the purchase data and every scanned item, giving each
    /// item its own internal serial number (guid). All or nothing - a single rejected serial
    /// number rejects the whole batch.
    /// </summary>
    ProductDeliveryVm Create(ProductDeliveryIm productDeliveryIm);

    ProductDeliveryVm Update(ProductDeliveryUm productDeliveryUm);

    /// <summary>Removes a delivery entered by mistake, together with its items.</summary>
    void Delete(Guid guid);
}
