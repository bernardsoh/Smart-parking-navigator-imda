namespace CarparkAvailability.WebApp.Models;

public sealed record CarparkSearchQuery(
    double Lat,
    double Lon,
    double RadiusMetres,
    string? VehicleType,
    bool HasAvailableLots,
    bool HasFreeParking,
    bool HasNightParking);

public sealed record CarparkSearchResponse(
    IReadOnlyList<CarparkResult> Items,
    int Total,
    DataStatus DataStatus);

public sealed record CarparkResult(
    string CarparkNo,
    string Address,
    double Latitude,
    double Longitude,
    double? DistanceMetres,
    string CarparkType,
    string ParkingSystem,
    string ShortTermParking,
    string FreeParking,
    string NightParking,
    int? CarparkDecks,
    double? GantryHeight,
    bool IsBasement,
    CarparkAvailabilityDetails Availability);

public sealed record CarparkAvailabilityDetails(
    DateTimeOffset? Timestamp,
    string Freshness,
    IReadOnlyList<CarparkLotAvailability> Lots);

public sealed record CarparkLotAvailability(
    string LotType,
    int TotalLots,
    int AvailableLots);

public sealed record DataStatus(
    string Freshness,
    DateTimeOffset? Timestamp);

public sealed record CarparkStatusResponse(
    string Freshness,
    DateTimeOffset? Timestamp,
    int TotalCarparks,
    int MatchedCarparks);
