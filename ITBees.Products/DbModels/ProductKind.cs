using System.Text.Json.Serialization;

namespace ITBees.Products.Entities;

/// <summary>
/// What a catalogue entry is - decides whether it can ever be on stock. Serialized by name for the
/// API clients. Goods is 0, so every row stored before the kinds existed is goods.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductKind
{
    /// <summary>"Towar" - a physical product received into warehouses, item by item or as a quantity.</summary>
    Goods = 0,

    /// <summary>
    /// "Usługa / abonament" - e.g. a licence or a subscription. It has no stock: deliveries and stock
    /// transfers refuse it. It may be valid for a limited time (<see cref="DbModels.Product.ValidityPeriod"/>).
    /// </summary>
    Service = 1
}
