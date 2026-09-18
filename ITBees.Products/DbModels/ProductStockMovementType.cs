using System.Text.Json.Serialization;

namespace ITBees.Products.Entities;

/// <summary>
/// Why the stock of a product kept without serial numbers changed. Serialized by name for the
/// API clients; starts at 1 so that a missing value is told apart from a real one.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductStockMovementType
{
    /// <summary>Pieces received in a purchase batch (<see cref="ProductDelivery"/>).</summary>
    Delivery = 1,

    /// <summary>Pieces taken out of a warehouse to another one - paired with a <see cref="TransferIn"/>.</summary>
    TransferOut = 2,

    /// <summary>Pieces brought in from another warehouse - paired with a <see cref="TransferOut"/>.</summary>
    TransferIn = 3,

    /// <summary>Pieces handed over to a customer - they leave the stock for good.</summary>
    HandOver = 4
}
