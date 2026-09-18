using ITBees.Interfaces.Repository;
using ITBees.Products.Entities;

namespace ITBees.Products.Services.Stock;

/// <summary>A product kept without serial numbers, in one warehouse.</summary>
internal readonly record struct StockKey(int ProductId, Guid WarehouseGuid);

/// <summary>Pieces a change needs to take out of a warehouse that are not there (any more).</summary>
internal sealed record StockShortage(StockKey Key, int Available, int Needed);

/// <summary>
/// Quantities of the products kept without serial numbers, summed from their movements. Shared
/// by everything that changes that stock, so the "never below zero" rule is checked the same way
/// for a purchase batch, a transfer and the undoing of either.
/// </summary>
internal static class StockBalances
{
    /// <summary>Current quantity of each pair; a pair without any movement is missing (= 0).</summary>
    public static Dictionary<StockKey, int> Current(IReadOnlyRepository<ProductStockMovement> movementRoRepo,
        IReadOnlyCollection<StockKey> keys)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<StockKey, int>();
        }

        var productIds = keys.Select(x => x.ProductId).Distinct().ToList();
        var warehouseGuids = keys.Select(x => x.WarehouseGuid).Distinct().ToList();

        // Summed by the database; the cross product of the ids may bring a few pairs too many.
        var wanted = keys.ToHashSet();
        return movementRoRepo
            .GetDataQueryable(x => productIds.Contains(x.ProductId) && warehouseGuids.Contains(x.WarehouseGuid))
            .GroupBy(x => new { x.ProductId, x.WarehouseGuid })
            .Select(x => new { x.Key.ProductId, x.Key.WarehouseGuid, Quantity = x.Sum(m => m.Quantity) })
            .ToList()
            .Select(x => (Key: new StockKey(x.ProductId, x.WarehouseGuid), x.Quantity))
            .Where(x => wanted.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Quantity);
    }

    /// <summary>Pairs that do not hold the quantities about to be taken out of them.</summary>
    public static List<StockShortage> Missing(IReadOnlyRepository<ProductStockMovement> movementRoRepo,
        IReadOnlyDictionary<StockKey, int> needed)
    {
        var current = Current(movementRoRepo, needed.Keys.ToList());
        return needed
            .Where(x => x.Value > 0)
            .Select(x => new StockShortage(x.Key, current.GetValueOrDefault(x.Key), x.Value))
            .Where(x => x.Available < x.Needed)
            .ToList();
    }

    /// <summary>
    /// Pairs that would drop below zero once the given movements are gone: pieces those movements
    /// brought in have been moved on or handed over since. Removing a movement that took pieces
    /// out only puts them back, so it never causes a shortage.
    /// </summary>
    public static List<StockShortage> AfterRemoving(IReadOnlyRepository<ProductStockMovement> movementRoRepo,
        IEnumerable<ProductStockMovement> movements)
    {
        var brought = movements
            .GroupBy(x => new StockKey(x.ProductId, x.WarehouseGuid))
            .ToDictionary(x => x.Key, x => x.Sum(m => m.Quantity));
        return Missing(movementRoRepo, brought);
    }

    /// <summary>"Papier termiczny 80 mm w magazynie „Biuro”: jest 5 szt., potrzeba 10" - one entry per shortage.</summary>
    public static string Describe(IEnumerable<StockShortage> shortages,
        IReadOnlyRepository<DbModels.Product> productRoRepo, IReadOnlyRepository<Warehouse> warehouseRoRepo)
    {
        var list = shortages.ToList();
        var productIds = list.Select(x => x.Key.ProductId).Distinct().ToList();
        var warehouseGuids = list.Select(x => x.Key.WarehouseGuid).Distinct().ToList();
        var products = productRoRepo.GetData(x => productIds.Contains(x.Id), x => x.Producer)
            .ToDictionary(x => x.Id, ProductName);
        var warehouses = warehouseRoRepo.GetData(x => warehouseGuids.Contains(x.Guid))
            .ToDictionary(x => x.Guid, x => x.WarehouseName);

        return string.Join("; ", list.Select(x =>
            $"{products.GetValueOrDefault(x.Key.ProductId, $"produkt {x.Key.ProductId}")} w magazynie " +
            $"„{warehouses.GetValueOrDefault(x.Key.WarehouseGuid, "?")}”: jest {Math.Max(0, x.Available)} szt., " +
            $"potrzeba {x.Needed}"));
    }

    /// <summary>"Producer — name", the way the admin panel names a catalogue entry.</summary>
    public static string ProductName(DbModels.Product product)
    {
        var name = (product.ShortDescription ?? string.Empty).Trim();
        var producer = (product.Producer?.Name ?? string.Empty).Trim();
        return producer.Length > 0 ? $"{producer} — {name}" : name;
    }
}
