using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.Deliveries;
using ITBees.Products.Controllers.Models.Stock;
using ITBees.Products.Entities;
using ITBees.Products.Services.Stock;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.UserManager.Interfaces;

namespace ITBees.Products.Services.Deliveries;

public class ProductDeliveryService : IProductDeliveryService
{
    private const int MaxItemsPerDelivery = 500;
    private const int MaxQuantityItemsPerDelivery = 200;
    private const int MaxSerialNumberLength = 200;
    private const int MaxWarrantyMonths = 240;
    private const int DefaultSellerSuggestions = 20;
    private const int MaxSellerSuggestions = 100;

    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IReadOnlyRepository<ProductDelivery> _deliveryRoRepo;
    private readonly IWriteOnlyRepository<ProductDelivery> _deliveryWoRepo;
    private readonly IReadOnlyRepository<SerializedProductOnStock> _stockRoRepo;
    private readonly IWriteOnlyRepository<SerializedProductOnStock> _stockWoRepo;
    private readonly IReadOnlyRepository<ProductStockMovement> _movementRoRepo;
    private readonly IWriteOnlyRepository<ProductStockMovement> _movementWoRepo;
    private readonly IReadOnlyRepository<DbModels.Product> _productRoRepo;
    private readonly IReadOnlyRepository<Warehouse> _warehouseRoRepo;
    private readonly ProductsSettings _settings;

    public ProductDeliveryService(
        IAspCurrentUserService aspCurrentUserService,
        IReadOnlyRepository<ProductDelivery> deliveryRoRepo,
        IWriteOnlyRepository<ProductDelivery> deliveryWoRepo,
        IReadOnlyRepository<SerializedProductOnStock> stockRoRepo,
        IWriteOnlyRepository<SerializedProductOnStock> stockWoRepo,
        IReadOnlyRepository<ProductStockMovement> movementRoRepo,
        IWriteOnlyRepository<ProductStockMovement> movementWoRepo,
        IReadOnlyRepository<DbModels.Product> productRoRepo,
        IReadOnlyRepository<Warehouse> warehouseRoRepo,
        ProductsSettings settings)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _deliveryRoRepo = deliveryRoRepo;
        _deliveryWoRepo = deliveryWoRepo;
        _stockRoRepo = stockRoRepo;
        _stockWoRepo = stockWoRepo;
        _movementRoRepo = movementRoRepo;
        _movementWoRepo = movementWoRepo;
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
        var result = _deliveryRoRepo
            .GetDataPaginated(x =>
                    (warehouses == null || warehouses.Contains(x.WarehouseGuid)) &&
                    (search == null ||
                     x.InvoiceNumber!.ToLower().Contains(search) ||
                     x.SellerName!.ToLower().Contains(search) ||
                     x.SellerNip!.Contains(search) ||
                     x.Notes!.ToLower().Contains(search) ||
                     x.Items.Any(i => i.SerialNumber.ToLower().Contains(search)) ||
                     // Products kept without serial numbers: by name, or by the EAN scanned into the box.
                     x.StockMovements.Any(m =>
                         m.Product!.ShortDescription.ToLower().Contains(search) || m.Product!.Ean.Contains(search))),
                sortOptions, x => x.Warehouse, x => x.Items);

        // Pieces counted by quantity - summed by the database for the deliveries of this page.
        var pageGuids = result.Data.Select(x => (Guid?)x.Guid).ToList();
        var units = pageGuids.Count == 0
            ? new Dictionary<Guid, int>()
            : _movementRoRepo.GetDataQueryable(x => pageGuids.Contains(x.ProductDeliveryGuid))
                .GroupBy(x => x.ProductDeliveryGuid)
                .Select(x => new { DeliveryGuid = x.Key, Units = x.Sum(m => m.Quantity) })
                .ToList()
                .ToDictionary(x => x.DeliveryGuid!.Value, x => x.Units);

        return result.MapTo(x =>
            new ProductDeliveryVm(x, x.Items?.Count ?? 0, unitsCount: units.GetValueOrDefault(x.Guid)));
    }

    public List<ProductDeliverySellerVm> GetSellers(string? search, int? limit)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
        var searchKey = search == null ? null : SellerNameKey(search);
        var take = Math.Clamp(limit ?? DefaultSellerSuggestions, 1, MaxSellerSuggestions);

        // Sellers live only inside the deliveries, and a delivery is one purchase - there are
        // few of them, so the matching ones are simply grouped in memory.
        var deliveries = _deliveryRoRepo.GetData(x =>
            x.SellerName != null &&
            (search == null ||
             x.SellerName.ToLower().Contains(search) ||
             (x.SellerNip != null && x.SellerNip.ToLower().Contains(search))));

        return deliveries
            .Where(x => !string.IsNullOrWhiteSpace(x.SellerName))
            .GroupBy(x => (Name: SellerNameKey(x.SellerName!), Nip: x.SellerNip ?? string.Empty))
            .Select(group => new
            {
                Latest = group.OrderByDescending(x => x.Created).ThenByDescending(x => x.PurchaseDate).First(),
                DeliveriesCount = group.Count(),
                LastPurchaseDate = group.Max(x => x.PurchaseDate)
            })
            // Names starting with the typed text come first, then the most recently used sellers.
            .OrderBy(x => searchKey == null ||
                          SellerNameKey(x.Latest.SellerName!).StartsWith(searchKey, StringComparison.Ordinal)
                ? 0
                : 1)
            .ThenByDescending(x => x.Latest.Created)
            .ThenBy(x => x.Latest.SellerName, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(x => new ProductDeliverySellerVm(x.Latest, x.DeliveriesCount, x.LastPurchaseDate))
            .ToList();
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
        var quantityItems = ValidateQuantityItems(productDeliveryIm.QuantityItems);
        if (items.Count == 0 && quantityItems.Count == 0)
        {
            throw new FasApiErrorException(
                "Zeskanuj przynajmniej jedno urządzenie albo kod EAN produktu bez numerów seryjnych.", 400);
        }

        var now = DateTime.Now;
        var createdBy = _aspCurrentUserService.GetCurrentUserGuid();
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
            CreatedByGuid = createdBy,
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
            }).ToList(),
            // The counted pieces are the stock itself - a movement into the warehouse per product.
            StockMovements = quantityItems.Select((item, index) => new ProductStockMovement
            {
                ProductId = item.ProductId,
                WarehouseGuid = warehouse.Guid,
                Quantity = item.Quantity,
                Type = ProductStockMovementType.Delivery,
                Position = index + 1,
                Created = now,
                CreatedByGuid = createdBy
            }).ToList()
        };

        // One insert of the whole graph = one transaction: either the delivery with all of
        // its items and counted pieces is stored, or nothing is.
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

        // Counted pieces are not told apart, so what matters is whether the warehouse still
        // holds as many as the delivery brought in - not where "these" pieces went.
        var movements = _movementRoRepo.GetData(x => x.ProductDeliveryGuid == guid).ToList();
        var shortages = StockBalances.AfterRemoving(_movementRoRepo, movements);
        if (shortages.Count > 0)
        {
            throw new FasApiErrorException(
                "Nie można usunąć dostawy - część sztuk produktów bez numerów seryjnych z niej przesunięto już " +
                $"do innego magazynu albo wydano: {StockBalances.Describe(shortages, _productRoRepo, _warehouseRoRepo)}.",
                400);
        }

        if (movements.Count > 0)
        {
            _movementWoRepo.DeleteData(x => x.ProductDeliveryGuid == guid);
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

        var quantityItems = _movementRoRepo
            .GetData(x => x.ProductDeliveryGuid == guid, x => x.Product!, x => x.Product!.Producer)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .Select(x => new ProductDeliveryQuantityItemVm(x))
            .ToList();

        return new ProductDeliveryVm(delivery, items.Count, items, quantityItems.Sum(x => x.Quantity), quantityItems);
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
    /// No items at all is fine here: a delivery may bring only products without serial numbers.
    /// </summary>
    private List<ProductDeliveryItemIm> ValidateItems(List<ProductDeliveryItemIm>? scannedItems)
    {
        if (scannedItems == null || scannedItems.Count == 0)
        {
            return new List<ProductDeliveryItemIm>();
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
        var knownProducts = _productRoRepo.GetData(x => productIds.Contains(x.Id), x => x.Producer)
            .ToDictionary(x => x.Id);
        var unknownProductIds = productIds.Where(x => !knownProducts.ContainsKey(x)).ToList();
        if (unknownProductIds.Count > 0)
        {
            throw new FasApiErrorException(
                $"Nie znaleziono produktu o identyfikatorze: {string.Join(", ", unknownProductIds)}.", 400);
        }

        var counted = items.FindIndex(x => knownProducts[x.ProductId].WithoutSerialNumbers);
        if (counted >= 0)
        {
            throw new FasApiErrorException(
                $"Pozycja {counted + 1}: produkt „{StockBalances.ProductName(knownProducts[items[counted].ProductId])}” " +
                "nie ma numerów seryjnych - jego sztuki liczy się, skanując kod EAN.", 400);
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

    /// <summary>
    /// Returns one line per product kept without serial numbers - lines of the same product added
    /// up, in the order the products were first scanned - or throws with the reason.
    /// </summary>
    private List<ProductDeliveryQuantityItemIm> ValidateQuantityItems(List<ProductDeliveryQuantityItemIm>? countedItems)
    {
        if (countedItems == null || countedItems.Count == 0)
        {
            return new List<ProductDeliveryQuantityItemIm>();
        }

        if (countedItems.Count > MaxQuantityItemsPerDelivery)
        {
            throw new FasApiErrorException(
                $"Jedna dostawa może zawierać najwyżej {MaxQuantityItemsPerDelivery} pozycji produktów bez numerów " +
                "seryjnych - podziel ją na części.", 400);
        }

        for (var i = 0; i < countedItems.Count; i++)
        {
            if (countedItems[i] == null || countedItems[i].ProductId <= 0)
            {
                throw new FasApiErrorException($"Produkty bez numerów seryjnych, pozycja {i + 1}: wybierz produkt.", 400);
            }

            if (countedItems[i].Quantity < 1)
            {
                throw new FasApiErrorException(
                    $"Produkty bez numerów seryjnych, pozycja {i + 1}: podaj liczbę sztuk - co najmniej 1.", 400);
            }
        }

        var merged = countedItems
            .GroupBy(x => x.ProductId)
            .Select(x => (ProductId: x.Key, Quantity: x.Sum(item => (long)item.Quantity)))
            .ToList();
        if (merged.Any(x => x.Quantity > ProductStockService.MaxQuantityPerLine))
        {
            throw new FasApiErrorException(
                $"Jedna dostawa może zawierać najwyżej {ProductStockService.MaxQuantityPerLine} szt. jednego produktu.",
                400);
        }

        var productIds = merged.Select(x => x.ProductId).ToList();
        var products = _productRoRepo.GetData(x => productIds.Contains(x.Id), x => x.Producer).ToDictionary(x => x.Id);
        var unknownProductIds = productIds.Where(x => !products.ContainsKey(x)).ToList();
        if (unknownProductIds.Count > 0)
        {
            throw new FasApiErrorException(
                $"Nie znaleziono produktu o identyfikatorze: {string.Join(", ", unknownProductIds)}.", 400);
        }

        var serialized = merged.Select(x => products[x.ProductId]).FirstOrDefault(x => !x.WithoutSerialNumbers);
        if (serialized != null)
        {
            throw new FasApiErrorException(
                $"Produkt „{StockBalances.ProductName(serialized)}” ma numery seryjne - zeskanuj numer seryjny " +
                "każdej sztuki zamiast liczyć je kodem EAN.", 400);
        }

        return merged
            .Select(x => new ProductDeliveryQuantityItemIm { ProductId = x.ProductId, Quantity = (int)x.Quantity })
            .ToList();
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// What makes two typed seller names the same seller: letter case and the amount of
    /// whitespace do not count.
    /// </summary>
    private static string SellerNameKey(string sellerName)
    {
        return string.Join(' ', sellerName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
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
