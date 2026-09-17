using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.Stock;

namespace ITBees.Products.Services.Stock;

public interface ISerializedProductService
{
    /// <summary>Item identified by our internal serial number - the guid behind the label QR code.</summary>
    SerializedProductVm Get(Guid guid);

    /// <param name="search">
    /// Manufacturer serial number, product, producer, invoice number or the code printed on the
    /// label. A scanned label (a link or a bare guid) finds exactly the labelled item.
    /// </param>
    PaginatedResult<SerializedProductVm> GetPaginated(string? search, Guid[]? warehouseGuids, int[]? productIds,
        Guid? productDeliveryGuid, bool? deliveredToEndCustomer, int? page, int? pageSize, string? sortColumn,
        SortOrder? sortOrder);

    /// <summary>Corrects the serial number, moves the item to another warehouse or marks it as handed over.</summary>
    SerializedProductVm Update(SerializedProductUm serializedProductUm);
}
