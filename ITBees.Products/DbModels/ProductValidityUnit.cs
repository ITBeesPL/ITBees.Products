using System.Text.Json.Serialization;

namespace ITBees.Products.Entities;

/// <summary>
/// Unit of the validity period of a service (<see cref="DbModels.Product.ValidityPeriod"/>). Serialized
/// by name for the API clients; starts at 1 so that a missing value is told apart from a real one.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductValidityUnit
{
    Day = 1,
    Month = 2,
    Year = 3
}
