namespace CarparkAvailability.ApiApp.Models;

public sealed record SearchRequest
{
    public double? Lat { get; init; }
    public double? Lon { get; init; }
    public double? RadiusMetres { get; init; }
    public string? VehicleType { get; init; }
    public bool? HasAvailableLots { get; init; }
    public bool? HasFreeParking { get; init; }
    public bool? HasNightParking { get; init; }
}
