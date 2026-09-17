using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.Deliveries;
using ITBees.Products.Controllers.Models.Stock;
using ITBees.Products.Entities;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.UserManager.Interfaces;

namespace ITBees.Products.Services.Deliveries;

public class ProductDeliveryService : IProductDeliveryService
{
    private const int MaxItemsPerDelivery = 500;
    private const int MaxSerialNumberLength = 200;
    private const int MaxWarrantyMonths = 240;

    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IReadOnlyRepository<ProductDelivery> _deliveryRoRepo;
    private readonly IWriteOnlyRepository<ProductDelivery> _deliveryWoRepo;
    private readonly IReadOnlyRepository<SerializedProductOnStock> _stockRoRepo;
    private readonly IWriteOnlyRepository<SerializedProductOnStock> _stockWoRepo;
    private readonly IReadOnlyRepository<DbModels.Product> _productRoRepo;
    private readonly IReadOnlyRepository<Warehouse> _warehouseRoRepo;
    private readonly ProductsSettings _settings;

    public ProductDeliveryService(
        IAspCurrentUserService aspCurrentUserService,
        IReadOnlyRepository<ProductDelivery> deliveryRoRepo,
        IWriteOnlyRepository<ProductDelivery> deliveryWoRepo,
        IReadOnlyRepository<SerializedProductOnStock> stockRoRepo,
        IWriteOnlyRepository<SerializedProductOnStock> stockWoRepo,
        IReadOnlyRepository<DbModels.Product> productRoRepo,
        IReadOnlyRepository<Warehouse> warehouseRoRepo,
        ProductsSettings settings)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _deliveryRoRepo = deliveryRoRepo;
        _deliveryWoRepo = deliveryWoRepo;
        _stockRoRepo = stockRoRepo;
        _stockWoRepo = stockWoRepo;
        _productRoRepo = productRoRepo;
        _warehouseRoRepo = warehouseRoRepo;
        _settings = settings;
    }

    public ProductDeliveryVm Get(Guid guid)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        return LoadWithItems(guid);
    }

    public PaginatedResult<ProductDeliveryVm> GetPaginated(string? search, Guid[]? warehouseGuids, int? page,
        int? pageSize, string? sortColumn, SortOrder? sortOrder)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var sortOptions = new SortOptions(page, pageSize, sortColumn ?? nameof(ProductDelivery.Created),
            sortOrder ?? SortOrder.Descending);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
        var warehouses = warehouseGuids?.Length > 0 ? warehouseGuids.Select(x => (Guid?)x).ToList() : null;

        // Items are loaded only to be counted - a page holds a few dozen deliveries at most.
        return _deliveryRoRepo
            .GetDataPaginated(x =>
                    (warehouses == null || warehouses.Contains(x.WarehouseGuid)) &&
                    (search == null ||
                     x.InvoiceNumber!.ToLower().Contains(search) ||
                     x.SellerName!.ToLower().Contains(search) ||
                     x.SellerNip!.Contains(search) ||
                     x.Notes!.ToLower().Contains(search) ||
                     x.Items.Any(i => i.SerialNumber.ToLower().Contains(search))),
                sortOptions, x => x.Warehouse, x => x.Items)
            .MapTo(x => new ProductDeliveryVm(x, x.Items?.Count ?? 0));
    }

    public ProductDeliveryVm Create(ProductDeliveryIm productDeliveryIm)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        if (productDeliveryIm == null)
        {
            throw new FasApiErrorException("Brak danych dostawy.", 400);
        }

        ValidatePurchaseData(productDeliveryIm.PurchaseDate, productDeliveryIm.WarrantyMonths);
        var warehouse = GetActiveWarehouseOrThrow(productDeliveryIm.WarehouseGuid);
        var items = ValidateItems(productDeliveryIm.Items);

        var now = DateTime.Now;
        var delivery = new ProductDelivery
        {
            Guid = Guid.NewGuid(),
            PurchaseDate = productDeliveryIm.PurchaseDate.Date,
            InvoiceNumber = TrimToNull(productDeliveryIm.InvoiceNumber),
            SellerNip = NormalizeNip(productDeliveryIm.SellerNip),
            SellerName = TrimToNull(productDeliveryIm.SellerName),
            SellerStreet = TrimToNull(productDeliveryIm.SellerStreet),
            SellerPostCode = TrimToNull(productDeliveryIm.SellerPostCode),
            SellerCity = TrimToNull(productDeliveryIm.SellerCity),
            WarrantyMonths = productDeliveryIm.WarrantyMonths,
            WarehouseGuid = warehouse.Guid,
            Notes = TrimToNull(productDeliveryIm.Notes),
            Created = now,
            CreatedByGuid = _aspCurrentUserService.GetCurrentUserGuid(),
            // Only foreign keys are set on the items - attaching loaded products or the
            // warehouse to the graph would make the repository try to insert them again.
            Items = items.Select((item, index) => new SerializedProductOnStock
            {
                Guid = Guid.NewGuid(),
                SerialNumber = item.SerialNumber,
                ProductId = item.ProductId,
                WarehouseGuid = warehouse.Guid,
                Received = now,
                DeliveredToEndCustomer = false,
                PositionInDelivery = index + 1
            }).ToList()
        };

        // One insert of the whole graph = one transaction: either the delivery and all of
        // its items are stored, or nothing is.
        _deliveryWoRepo.InsertData(delivery);

        return LoadWithItems(delivery.Guid);
    }

    public ProductDeliveryVm Update(ProductDeliveryUm productDeliveryUm)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        if (productDeliveryUm == null)
        {
            throw new FasApiErrorException("Brak danych dostawy.", 400);
        }

        ValidatePurchaseData(productDeliveryUm.PurchaseDate, productDeliveryUm.WarrantyMonths);

        var updated = _deliveryWoRepo.UpdateData(x => x.Guid == productDeliveryUm.Guid, x =>
        {
            x.PurchaseDate = productDeliveryUm.PurchaseDate.Date;
            x.InvoiceNumber = TrimToNull(productDeliveryUm.InvoiceNumber);
            x.SellerNip = NormalizeNip(productDeliveryUm.SellerNip);
            x.SellerName = TrimToNull(productDeliveryUm.SellerName);
            x.SellerStreet = TrimToNull(productDeliveryUm.SellerStreet);
            x.SellerPostCode = TrimToNull(productDeliveryUm.SellerPostCode);
            x.SellerCity = TrimToNull(productDeliveryUm.SellerCity);
            x.WarrantyMonths = productDeliveryUm.WarrantyMonths;
            x.Notes = TrimToNull(productDeliveryUm.Notes);
        }).FirstOrDefault();

        if (updated == null)
        {
            throw new FasApiErrorException("Nie znaleziono dostawy.", 404);
        }

        return LoadWithItems(updated.Guid);
    }

    public void Delete(Guid guid)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        if (!_deliveryRoRepo.HasData(x => x.Guid == guid))
        {
            throw new FasApiErrorException("Nie znaleziono dostawy.", 404);
        }

        // A device that already left to a customer is part of someone's history - the batch
        // it came from must not disappear.
        var handedOver = _stockRoRepo.GetDataCount(x => x.ProductDeliveryGuid == guid && x.DeliveredToEndCustomer);
        if (handedOver > 0)
        {
            throw new FasApiErrorException(
                $"Nie można usunąć dostawy - {handedOver} szt. z niej wydano już do klienta.", 400);
        }

        _stockWoRepo.DeleteData(x => x.ProductDeliveryGuid == guid);
        _deliveryWoRepo.DeleteData(x => x.Guid == guid);
    }

    private ProductDeliveryVm LoadWithItems(Guid guid)
    {
        var delivery = _deliveryRoRepo.GetData(x => x.Guid == guid, x => x.Warehouse).FirstOrDefault();
        if (delivery == null)
        {
            throw new FasApiErrorException("Nie znaleziono dostawy.", 404);
        }

        var items = _stockRoRepo
            .GetData(x => x.ProductDeliveryGuid == guid, x => x.Product, x => x.Product.Producer,
                x => x.Warehouse, x => x.ProductDelivery)
            .OrderBy(x => x.PositionInDelivery)
            .ThenBy(x => x.Id)
            .Select(x => new SerializedProductVm(x, _settings))
            .ToList();

        return new ProductDeliveryVm(delivery, items.Count, items);
    }

    private static void ValidatePurchaseData(DateTime purchaseDate, int warrantyMonths)
    {
        if (purchaseDate == default)
        {
            throw new FasApiErrorException("Podaj datę zakupu.", 400);
        }

        if (warrantyMonths < 0 || warrantyMonths > MaxWarrantyMonths)
        {
            throw new FasApiErrorException(
                $"Okres gwarancji podaj w miesiącach, od 0 do {MaxWarrantyMonths}.", 400);
        }
    }

    private Warehouse GetActiveWarehouseOrThrow(Guid warehouseGuid)
    {
        var warehouse = _warehouseRoRepo.GetData(x => x.Guid == warehouseGuid).FirstOrDefault();
        if (warehouse == null)
        {
            throw new FasApiErrorException("Wybierz magazyn, na który trafia dostawa.", 400);
        }

        if (!warehouse.IsActive)
        {
            throw new FasApiErrorException(
                $"Magazyn „{warehouse.WarehouseName}” jest nieaktywny - wybierz inny.", 400);
        }

        return warehouse;
    }

    /// <summary>
    /// Returns the items with trimmed serial numbers, in the original (scanning) order, or
    /// throws naming every offending serial number - the operator fixes the list in one go.
    /// </summary>
    private List<ProductDeliveryItemIm> ValidateItems(List<ProductDeliveryItemIm>? scannedItems)
    {
        if (scannedItems == null || scannedItems.Count == 0)
        {
            throw new FasApiErrorException("Zeskanuj przynajmniej jedno urządzenie.", 400);
        }

        if (scannedItems.Count > MaxItemsPerDelivery)
        {
            throw new FasApiErrorException(
                $"Jedna dostawa może zawierać najwyżej {MaxItemsPerDelivery} urządzeń - podziel ją na części.", 400);
        }

        var items = scannedItems
            .Select(x => new ProductDeliveryItemIm
            {
                ProductId = x?.ProductId ?? 0,
                SerialNumber = (x?.SerialNumber ?? string.Empty).Trim()
            })
            .ToList();

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].ProductId <= 0)
            {
                throw new FasApiErrorException($"Pozycja {i + 1}: wybierz produkt.", 400);
            }

            if (items[i].SerialNumber.Length == 0)
            {
                throw new FasApiErrorException($"Pozycja {i + 1}: brak numeru seryjnego.", 400);
            }

            if (items[i].SerialNumber.Length > MaxSerialNumberLength)
            {
                throw new FasApiErrorException(
                    $"Pozycja {i + 1}: numer seryjny jest dłuższy niż {MaxSerialNumberLength} znaków.", 400);
            }
        }

        var scannedTwice = items
            .GroupBy(x => (x.ProductId, SerialNumber: x.SerialNumber.ToUpperInvariant()))
            .Where(x => x.Count() > 1)
            .Select(x => x.First().SerialNumber)
            .ToList();
        if (scannedTwice.Count > 0)
        {
            throw new FasApiErrorException(
                $"Numery seryjne zeskanowane więcej niż raz: {string.Join(", ", scannedTwice)}.", 400);
        }

        var productIds = items.Select(x => x.ProductId).Distinct().ToList();
        var knownProductIds = _productRoRepo.GetData(x => productIds.Contains(x.Id)).Select(x => x.Id).ToHashSet();
        var unknownProductIds = productIds.Where(x => !knownProductIds.Contains(x)).ToList();
        if (unknownProductIds.Count > 0)
        {
            throw new FasApiErrorException(
                $"Nie znaleziono produktu o identyfikatorze: {string.Join(", ", unknownProductIds)}.", 400);
        }

        // The same serial number may legitimately exist for two different products, so the
        // database match is narrowed down to the product in memory.
        var serialNumbers = items.Select(x => x.SerialNumber).Distinct().ToList();
        var alreadyOnStock = _stockRoRepo
            .GetData(x => serialNumbers.Contains(x.SerialNumber))
            .Where(existing => items.Any(x =>
                x.ProductId == existing.ProductId &&
                string.Equals(x.SerialNumber, existing.SerialNumber, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.SerialNumber)
            .Distinct()
            .ToList();
        if (alreadyOnStock.Count > 0)
        {
            throw new FasApiErrorException(
                $"Te numery seryjne są już wprowadzone do magazynu: {string.Join(", ", alreadyOnStock)}.", 409);
        }

        return items;
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Keeps only the digits of a Polish NIP; anything else (e.g. a foreign VAT id) stays as typed.</summary>
    private static string? NormalizeNip(string? nip)
    {
        var trimmed = TrimToNull(nip);
        if (trimmed == null)
        {
            return null;
        }

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        var looksPolish = digits.Length == 10 &&
                          trimmed.All(c => char.IsDigit(c) || c is '-' or ' ' || c is 'P' or 'L' or 'p' or 'l');
        return looksPolish ? digits : trimmed;
    }
}
