using CsvHelper.Configuration.Attributes;

namespace CarparkAvailability.ApiApp.Models;

public sealed record HdbCarpark
{
    [Name("car_park_no")]
    public string CarParkNo { get; init; } = string.Empty;

    [Name("address")]
    public string Address { get; init; } = string.Empty;

    [Name("x_coord")]
    public double XCoord { get; init; }

    [Name("y_coord")]
    public double YCoord { get; init; }

    [Name("car_park_type")]
    public string CarParkType { get; init; } = string.Empty;

    [Name("type_of_parking_system")]
    public string TypeOfParkingSystem { get; init; } = string.Empty;

    [Name("short_term_parking")]
    public string ShortTermParking { get; init; } = string.Empty;

    [Name("free_parking")]
    public string FreeParking { get; init; } = string.Empty;

    [Name("night_parking")]
    public string NightParking { get; init; } = string.Empty;

    [Name("car_park_decks")]
    public int? CarParkDecks { get; init; }

    [Name("gantry_height")]
    public double? GantryHeight { get; init; }

    [Name("car_park_basement")]
    public string CarParkBasement { get; init; } = string.Empty;

    [Ignore]
    public double Latitude { get; init; }

    [Ignore]
    public double Longitude { get; init; }
}
