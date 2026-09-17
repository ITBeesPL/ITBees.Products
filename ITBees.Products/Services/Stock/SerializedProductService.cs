using System.Text.RegularExpressions;
using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.Stock;
using ITBees.Products.Entities;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.UserManager.Interfaces;

namespace ITBees.Products.Services.Stock;

public class SerializedProductService : ISerializedProductService
{
    private const int MaxSerialNumberLength = 200;

    private static readonly Regex GuidPattern = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);

    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IReadOnlyRepository<SerializedProductOnStock> _stockRoRepo;
    private readonly IWriteOnlyRepository<SerializedProductOnStock> _stockWoRepo;
    private readonly IReadOnlyRepository<Warehouse> _warehouseRoRepo;
    private readonly ProductsSettings _settings;

    public SerializedProductService(
        IAspCurrentUserService aspCurrentUserService,
        IReadOnlyRepository<SerializedProductOnStock> stockRoRepo,
        IWriteOnlyRepository<SerializedProductOnStock> stockWoRepo,
        IReadOnlyRepository<Warehouse> warehouseRoRepo,
        ProductsSettings settings)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _stockRoRepo = stockRoRepo;
        _stockWoRepo = stockWoRepo;
        _warehouseRoRepo = warehouseRoRepo;
        _settings = settings;
    }

    public SerializedProductVm Get(Guid guid)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        return Load(guid);
    }

    public PaginatedResult<SerializedProductVm> GetPaginated(string? search, Guid[]? warehouseGuids, int[]? productIds,
        Guid? productDeliveryGuid, bool? deliveredToEndCustomer, int? page, int? pageSize, string? sortColumn,
        SortOrder? sortOrder)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        // Newest first; the id also keeps the items of one delivery together.
        var sortOptions = new SortOptions(page, pageSize, sortColumn ?? nameof(SerializedProductOnStock.Id),
            sortOrder ?? SortOrder.Descending);

        // A label scanned into the search box yields a link or a bare guid - match that one item.
        var scannedGuid = TryExtractGuid(search);
        var text = scannedGuid != null || string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();

        // Empty filter lists mean "no filter", exactly like missing ones.
        var warehouses = warehouseGuids?.Length > 0 ? warehouseGuids.Select(x => (Guid?)x).ToList() : null;
        var products = productIds?.Length > 0 ? productIds.ToList() : null;

        return _stockRoRepo
            .GetDataPaginated(x =>
                    (warehouses == null || warehouses.Contains(x.WarehouseGuid)) &&
                    (products == null || products.Contains(x.ProductId)) &&
                    (productDeliveryGuid == null || x.ProductDeliveryGuid == productDeliveryGuid) &&
                    (deliveredToEndCustomer == null || x.DeliveredToEndCustomer == deliveredToEndCustomer) &&
                    (scannedGuid == null || x.Guid == scannedGuid) &&
                    (text == null ||
                     x.SerialNumber.ToLower().Contains(text) ||
                     x.Product.ShortDescription.ToLower().Contains(text) ||
                     x.Product.Producer.Name.ToLower().Contains(text) ||
                     x.ProductDelivery!.InvoiceNumber!.ToLower().Contains(text) ||
                     // The code printed on the label is the tail of the guid.
                     x.Guid!.ToString()!.Contains(text)),
                sortOptions, x => x.Product, x => x.Product.Producer, x => x.Warehouse, x => x.ProductDelivery)
            .MapTo(x => new SerializedProductVm(x, _settings));
    }

    public SerializedProductVm Update(SerializedProductUm serializedProductUm)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        if (serializedProductUm == null)
        {
            throw new FasApiErrorException("Brak danych urządzenia.", 400);
        }

        var serialNumber = (serializedProductUm.SerialNumber ?? string.Empty).Trim();
        if (serialNumber.Length == 0)
        {
            throw new FasApiErrorException("Podaj numer seryjny urządzenia.", 400);
        }

        if (serialNumber.Length > MaxSerialNumberLength)
        {
            throw new FasApiErrorException(
                $"Numer seryjny może mieć najwyżej {MaxSerialNumberLength} znaków.", 400);
        }

        if (serializedProductUm.WarehouseGuid.HasValue)
        {
            var warehouse = _warehouseRoRepo.GetData(x => x.Guid == serializedProductUm.WarehouseGuid)
                .FirstOrDefault();
            if (warehouse == null)
            {
                throw new FasApiErrorException("Nie znaleziono wybranego magazynu.", 400);
            }
        }

        var updated = _stockWoRepo.UpdateData(x => x.Guid == serializedProductUm.Guid, x =>
        {
            // Checked here, where the product of the edited item is finally known.
            if (!string.Equals(x.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase) &&
                _stockRoRepo.HasData(other =>
                    other.Id != x.Id && other.ProductId == x.ProductId && other.SerialNumber == serialNumber))
            {
                throw new FasApiErrorException(
                    $"Numer seryjny {serialNumber} jest już wprowadzony do magazynu dla tego produktu.", 409);
            }

            x.SerialNumber = serialNumber;
            x.WarehouseGuid = serializedProductUm.WarehouseGuid;
            x.DeliveredToEndCustomer = serializedProductUm.DeliveredToEndCustomer;
        }).FirstOrDefault();

        if (updated == null)
        {
            throw new FasApiErrorException("Nie znaleziono urządzenia w magazynie.", 404);
        }

        return Load(serializedProductUm.Guid);
    }

    private SerializedProductVm Load(Guid guid)
    {
        var item = _stockRoRepo
            .GetData(x => x.Guid == guid, x => x.Product, x => x.Product.Producer, x => x.Warehouse,
                x => x.ProductDelivery)
            .FirstOrDefault();
        if (item == null)
        {
            throw new FasApiErrorException("Nie znaleziono urządzenia w magazynie.", 404);
        }

        return new SerializedProductVm(item, _settings);
    }

    private static Guid? TryExtractGuid(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var match = GuidPattern.Match(search);
        return match.Success && Guid.TryParse(match.Value, out var guid) ? guid : null;
    }
}
