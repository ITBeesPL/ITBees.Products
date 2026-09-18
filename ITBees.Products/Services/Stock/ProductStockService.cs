using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.Stock;
using ITBees.Products.Entities;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.UserManager.Interfaces;

namespace ITBees.Products.Services.Stock;

public class ProductStockService : IProductStockService
{
    /// <summary>Most pieces of one product a single document (a delivery, a transfer) may carry.</summary>
    public const int MaxQuantityPerLine = 100_000;

    private const int MaxLinesPerTransfer = 200;
    private const int MaxDocumentNameLength = 200;

    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IReadOnlyRepository<ProductStockMovement> _movementRoRepo;
    private readonly IWriteOnlyRepository<ProductStockMovement> _movementWoRepo;
    private readonly IReadOnlyRepository<SerializedProductOnStock> _stockRoRepo;
    private readonly IReadOnlyRepository<DbModels.Product> _productRoRepo;
    private readonly IReadOnlyRepository<Warehouse> _warehouseRoRepo;

    public ProductStockService(
        IAspCurrentUserService aspCurrentUserService,
        IReadOnlyRepository<ProductStockMovement> movementRoRepo,
        IWriteOnlyRepository<ProductStockMovement> movementWoRepo,
        IReadOnlyRepository<SerializedProductOnStock> stockRoRepo,
        IReadOnlyRepository<DbModels.Product> productRoRepo,
        IReadOnlyRepository<Warehouse> warehouseRoRepo)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _movementRoRepo = movementRoRepo;
        _movementWoRepo = movementWoRepo;
        _stockRoRepo = stockRoRepo;
        _productRoRepo = productRoRepo;
        _warehouseRoRepo = warehouseRoRepo;
    }

    public List<ProductStockLevelVm> GetLevels(string? search, Guid[]? warehouseGuids, int[]? productIds,
        bool? withoutSerialNumbers, bool? includeEmpty)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        // Empty filter lists mean "no filter", exactly like missing ones.
        var warehouses = warehouseGuids?.Length > 0 ? warehouseGuids.Distinct().ToList() : null;
        var products = productIds?.Length > 0 ? productIds.Distinct().ToList() : null;
        var levels = new List<(int ProductId, Guid? WarehouseGuid, int Quantity, DateTime? LastChange)>();

        if (withoutSerialNumbers != false)
        {
            levels.AddRange(_movementRoRepo
                .GetDataQueryable(x => (warehouses == null || warehouses.Contains(x.WarehouseGuid)) &&
                                       (products == null || products.Contains(x.ProductId)))
                .GroupBy(x => new { x.ProductId, x.WarehouseGuid })
                .Select(x => new
                {
                    x.Key.ProductId,
                    x.Key.WarehouseGuid,
                    Quantity = x.Sum(m => m.Quantity),
                    LastChange = x.Max(m => m.Created)
                })
                .ToList()
                .Select(x => (x.ProductId, (Guid?)x.WarehouseGuid, x.Quantity, (DateTime?)x.LastChange)));
        }

        if (withoutSerialNumbers != true)
        {
            var itemWarehouses = warehouses?.Select(x => (Guid?)x).ToList();
            levels.AddRange(_stockRoRepo
                .GetDataQueryable(x => !x.DeliveredToEndCustomer &&
                                       (itemWarehouses == null || itemWarehouses.Contains(x.WarehouseGuid)) &&
                                       (products == null || products.Contains(x.ProductId)))
                .GroupBy(x => new { x.ProductId, x.WarehouseGuid })
                .Select(x => new
                {
                    x.Key.ProductId,
                    x.Key.WarehouseGuid,
                    Quantity = x.Count(),
                    LastChange = x.Max(i => i.Received)
                })
                .ToList()
                .Select(x => (x.ProductId, x.WarehouseGuid, x.Quantity, (DateTime?)x.LastChange)));
        }

        if (includeEmpty != true)
        {
            levels = levels.Where(x => x.Quantity != 0).ToList();
        }

        var foundProductIds = levels.Select(x => x.ProductId).Distinct().ToList();
        var productsById = foundProductIds.Count == 0
            ? new Dictionary<int, DbModels.Product>()
            : _productRoRepo.GetData(x => foundProductIds.Contains(x.Id), x => x.Producer).ToDictionary(x => x.Id);
        var foundWarehouseGuids = levels.Where(x => x.WarehouseGuid.HasValue)
            .Select(x => x.WarehouseGuid!.Value)
            .Distinct()
            .ToList();
        var warehouseNames = foundWarehouseGuids.Count == 0
            ? new Dictionary<Guid, string>()
            : _warehouseRoRepo.GetData(x => foundWarehouseGuids.Contains(x.Guid))
                .ToDictionary(x => x.Guid, x => x.WarehouseName);

        var text = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return levels
            .Select(x => new ProductStockLevelVm(productsById.GetValueOrDefault(x.ProductId), x.ProductId,
                x.WarehouseGuid,
                x.WarehouseGuid.HasValue ? warehouseNames.GetValueOrDefault(x.WarehouseGuid.Value) : null,
                x.Quantity, x.LastChange))
            .Where(x => text == null ||
                        new[] { x.ProductName, x.ProducerName, x.Ean, x.WarehouseName }.Any(value =>
                            value != null && value.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.ProducerName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ProductName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.WarehouseName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ProductStockTransfer PrepareTransfer(IEnumerable<ProductStockTransferRequest>? requests,
        Guid? targetWarehouseGuid)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var requested = (requests ?? Enumerable.Empty<ProductStockTransferRequest>()).ToList();
        if (requested.Count == 0)
        {
            return new ProductStockTransfer(targetWarehouseGuid, new List<ProductStockTransferLine>());
        }

        if (requested.Count > MaxLinesPerTransfer)
        {
            throw new FasApiErrorException(
                $"Jedno przesunięcie może obejmować najwyżej {MaxLinesPerTransfer} pozycji produktów bez numerów seryjnych.",
                400);
        }

        for (var i = 0; i < requested.Count; i++)
        {
            var request = requested[i];
            if (request == null || request.ProductId <= 0)
            {
                throw new FasApiErrorException($"Pozycja {i + 1}: wybierz produkt.", 400);
            }

            if (request.SourceWarehouseGuid is null || request.SourceWarehouseGuid == Guid.Empty)
            {
                throw new FasApiErrorException(
                    $"Pozycja {i + 1}: wybierz magazyn, z którego zabierasz sztuki.", 400);
            }

            if (request.Quantity < 1)
            {
                throw new FasApiErrorException($"Pozycja {i + 1}: podaj liczbę sztuk - co najmniej 1.", 400);
            }
        }

        // One line per product and source warehouse - a product scanned in twice is added up.
        // GroupBy keeps the order in which the keys first appear, i.e. the scanning order.
        var merged = requested
            .GroupBy(x => new StockKey(x.ProductId, x.SourceWarehouseGuid!.Value))
            .Select(x => (x.Key, Quantity: x.Sum(r => (long)r.Quantity)))
            .ToList();
        if (merged.Any(x => x.Quantity > MaxQuantityPerLine))
        {
            throw new FasApiErrorException(
                $"Jedno przesunięcie może obejmować najwyżej {MaxQuantityPerLine} szt. jednego produktu z jednego magazynu.",
                400);
        }

        var productIds = merged.Select(x => x.Key.ProductId).Distinct().ToList();
        var products = _productRoRepo.GetData(x => productIds.Contains(x.Id), x => x.Producer)
            .ToDictionary(x => x.Id);
        var unknownProductIds = productIds.Where(x => !products.ContainsKey(x)).ToList();
        if (unknownProductIds.Count > 0)
        {
            throw new FasApiErrorException(
                $"Nie znaleziono produktu o identyfikatorze: {string.Join(", ", unknownProductIds)}.", 400);
        }

        var serialized = products.Values.FirstOrDefault(x => !x.WithoutSerialNumbers);
        if (serialized != null)
        {
            throw new FasApiErrorException(
                $"Produkt „{StockBalances.ProductName(serialized)}” ma numery seryjne - przenosi się konkretne " +
                "sztuki (po ich etykietach), nie liczbę sztuk.", 400);
        }

        var warehouseGuids = merged.Select(x => x.Key.WarehouseGuid).Distinct().ToList();
        if (targetWarehouseGuid.HasValue)
        {
            warehouseGuids.Add(targetWarehouseGuid.Value);
        }

        var warehouses = _warehouseRoRepo.GetData(x => warehouseGuids.Contains(x.Guid)).ToDictionary(x => x.Guid);
        if (merged.Any(x => !warehouses.ContainsKey(x.Key.WarehouseGuid)))
        {
            throw new FasApiErrorException("Nie znaleziono magazynu, z którego mają zostać zabrane sztuki.", 400);
        }

        if (targetWarehouseGuid.HasValue)
        {
            if (!warehouses.TryGetValue(targetWarehouseGuid.Value, out var target))
            {
                throw new FasApiErrorException("Nie znaleziono magazynu docelowego.", 400);
            }

            var toItself = merged.Where(x => x.Key.WarehouseGuid == target.Guid).Select(x => x.Key.ProductId).ToList();
            if (toItself.Count > 0)
            {
                throw new FasApiErrorException(
                    $"Produkt „{StockBalances.ProductName(products[toItself[0]])}” miałby trafić z magazynu " +
                    $"„{target.WarehouseName}” do niego samego - wybierz inny magazyn źródłowy.", 400);
            }
        }

        var shortages = StockBalances.Missing(_movementRoRepo, merged.ToDictionary(x => x.Key, x => (int)x.Quantity));
        if (shortages.Count > 0)
        {
            throw new FasApiErrorException(
                $"Za mało sztuk na stanie - {StockBalances.Describe(shortages, _productRoRepo, _warehouseRoRepo)}.",
                400);
        }

        var lines = merged.Select((x, index) =>
        {
            var product = products[x.Key.ProductId];
            return new ProductStockTransferLine
            {
                Position = index + 1,
                ProductId = product.Id,
                ProductName = product.ShortDescription ?? string.Empty,
                ProducerName = product.Producer?.Name,
                Ean = string.IsNullOrWhiteSpace(product.Ean) ? null : product.Ean,
                SourceWarehouseGuid = x.Key.WarehouseGuid,
                SourceWarehouseName = warehouses[x.Key.WarehouseGuid].WarehouseName,
                Quantity = (int)x.Quantity
            };
        }).ToList();

        return new ProductStockTransfer(targetWarehouseGuid, lines);
    }

    public void Transfer(ProductStockTransfer transfer, Guid documentGuid, string? documentName)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        if (transfer.Lines.Count == 0)
        {
            return;
        }

        if (_movementRoRepo.HasData(x => x.DocumentGuid == documentGuid))
        {
            throw new FasApiErrorException("Ten dokument ma już zapisane ruchy magazynowe.", 409);
        }

        var now = DateTime.Now;
        var createdBy = _aspCurrentUserService.GetCurrentUserGuid();
        var name = documentName?.Trim() is { Length: > 0 } trimmed
            ? trimmed.Length > MaxDocumentNameLength ? trimmed[..MaxDocumentNameLength] : trimmed
            : null;
        var target = transfer.TargetWarehouseGuid;

        var movements = new List<ProductStockMovement>();
        foreach (var line in transfer.Lines)
        {
            // Only foreign keys are set - attached products or warehouses would be inserted again.
            movements.Add(new ProductStockMovement
            {
                ProductId = line.ProductId,
                WarehouseGuid = line.SourceWarehouseGuid,
                Quantity = -line.Quantity,
                Type = target.HasValue ? ProductStockMovementType.TransferOut : ProductStockMovementType.HandOver,
                DocumentGuid = documentGuid,
                DocumentName = name,
                Position = line.Position,
                Created = now,
                CreatedByGuid = createdBy
            });

            if (target.HasValue)
            {
                movements.Add(new ProductStockMovement
                {
                    ProductId = line.ProductId,
                    WarehouseGuid = target.Value,
                    Quantity = line.Quantity,
                    Type = ProductStockMovementType.TransferIn,
                    DocumentGuid = documentGuid,
                    DocumentName = name,
                    Position = line.Position,
                    Created = now,
                    CreatedByGuid = createdBy
                });
            }
        }

        // One insert of all the lines = one transaction: the document moves everything or nothing.
        _movementWoRepo.InsertData(movements);

        // The stock was checked in PrepareTransfer, but another operator may have taken the same
        // pieces since. Whoever finds the stock below zero afterwards backs out - of two such
        // transfers at least one does, and the stock never stays negative.
        var sources = transfer.Lines.Select(x => new StockKey(x.ProductId, x.SourceWarehouseGuid)).Distinct().ToList();
        if (StockBalances.Current(_movementRoRepo, sources).Values.Any(x => x < 0))
        {
            _movementWoRepo.DeleteData(x => x.DocumentGuid == documentGuid);
            throw new FasApiErrorException(
                "Stan magazynu zmienił się w międzyczasie - ktoś zabrał część tych samych sztuk. " +
                "Odśwież stan i spróbuj ponownie.", 409);
        }
    }

    public IReadOnlyList<ProductStockMovement> Revert(Guid documentGuid)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var movements = _movementRoRepo.GetData(x => x.DocumentGuid == documentGuid).ToList();
        if (movements.Count == 0)
        {
            return movements;
        }

        var shortages = StockBalances.AfterRemoving(_movementRoRepo, movements);
        if (shortages.Count > 0)
        {
            var name = movements.Select(x => x.DocumentName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            throw new FasApiErrorException(
                $"Nie można cofnąć{(name == null ? string.Empty : $" „{name}”")} - część sztuk produktów bez " +
                "numerów seryjnych została od tego czasu przesunięta dalej albo wydana: " +
                $"{StockBalances.Describe(shortages, _productRoRepo, _warehouseRoRepo)}.", 400);
        }

        _movementWoRepo.DeleteData(x => x.DocumentGuid == documentGuid);
        return movements;
    }

    public void Restore(IEnumerable<ProductStockMovement> movements)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var copies = movements.Select(x => new ProductStockMovement
        {
            ProductId = x.ProductId,
            WarehouseGuid = x.WarehouseGuid,
            Quantity = x.Quantity,
            Type = x.Type,
            ProductDeliveryGuid = x.ProductDeliveryGuid,
            DocumentGuid = x.DocumentGuid,
            DocumentName = x.DocumentName,
            Position = x.Position,
            Created = x.Created,
            CreatedByGuid = x.CreatedByGuid
        }).ToList();

        if (copies.Count > 0)
        {
            _movementWoRepo.InsertData(copies);
        }
    }
}
