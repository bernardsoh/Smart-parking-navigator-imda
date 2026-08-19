using System.Text.Json.Serialization;

namespace CarparkAvailability.ApiApp.Models;

public sealed record DataGovSgAvailabilityResponse(
    [property: JsonPropertyName("api_info")] ApiInfo? ApiInfo,
    [property: JsonPropertyName("items")] IReadOnlyList<AvailabilityItem>? Items);

public sealed record ApiInfo(
    [property: JsonPropertyName("status")] string? Status);

public sealed record AvailabilityItem(
    [property: JsonPropertyName("timestamp")] DateTimeOffset? Timestamp,
    [property: JsonPropertyName("carpark_data")] IReadOnlyList<LiveAvailability>? CarparkData);

public sealed record LiveAvailability(
    [property: JsonPropertyName("carpark_number")] string? CarparkNumber,
    [property: JsonPropertyName("update_datetime")] string? UpdateDateTime,
    [property: JsonPropertyName("carpark_info")] IReadOnlyList<LiveLotAvailability>? CarparkInfo);

public sealed record LiveLotAvailability(
    [property: JsonPropertyName("total_lots")] string? TotalLots,
    [property: JsonPropertyName("lot_type")] string? LotType,
    [property: JsonPropertyName("lots_available")] string? LotsAvailable);

public sealed record AvailabilitySnapshot(
    DateTimeOffset? Timestamp,
    IReadOnlyDictionary<string, LiveAvailability> Carparks,
    bool IsFallback,
    string? SourceStatus)
{
    public static AvailabilitySnapshot Empty { get; } = new(null, new Dictionary<string, LiveAvailability>(StringComparer.OrdinalIgnoreCase), true, null);
}
