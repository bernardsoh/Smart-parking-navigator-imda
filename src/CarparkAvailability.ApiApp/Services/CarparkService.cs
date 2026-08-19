using CarparkAvailability.ApiApp.Models;
using Microsoft.Extensions.Options;

namespace CarparkAvailability.ApiApp.Services;

public sealed class CarparkService
{
    private readonly Lazy<IReadOnlyDictionary<string, HdbCarpark>> _carparks;
    private readonly HdbCsvParser _csvParser;
    private readonly IDataGovSgClient _dataGovSgClient;
    private readonly ILogger<CarparkService> _logger;
    private readonly StaticDataOptions _options;
    private readonly IHostEnvironment _hostEnvironment;

    public CarparkService(
        HdbCsvParser csvParser,
        IDataGovSgClient dataGovSgClient,
        IOptions<StaticDataOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<CarparkService> logger)
    {
        _csvParser = csvParser;
        _dataGovSgClient = dataGovSgClient;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _options = options.Value;
        _carparks = new Lazy<IReadOnlyDictionary<string, HdbCarpark>>(LoadCarparks, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<CarparkResult?> GetCarparkAsync(string carparkNo, CancellationToken cancellationToken = default)
        => GetCarparkCoreAsync(carparkNo, cancellationToken);

    public async Task<CarparkSearchResponse> SearchNearbyAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, HdbCarpark> carparks = _carparks.Value;
        AvailabilitySnapshot snapshot = await _dataGovSgClient.GetAvailabilityAsync(cancellationToken);
        string? vehicleType = NormalizeVehicleType(request.VehicleType);
        string freshness = DetermineFreshness(snapshot.Timestamp, snapshot.IsFallback);
        double radiusMetres = request.RadiusMetres ?? 500;

        List<CarparkResult> items = new();
        foreach (HdbCarpark carpark in carparks.Values)
        {
            double distance = Svy21Converter.DistanceMetres(request.Lat!.Value, request.Lon!.Value, carpark.Latitude, carpark.Longitude);
            if (distance > radiusMetres)
            {
                continue;
            }

            if (request.HasFreeParking == true && !IsEnabledFlag(carpark.FreeParking))
            {
                continue;
            }

            if (request.HasNightParking == true && !IsEnabledFlag(carpark.NightParking))
            {
                continue;
            }

            LiveAvailability? liveAvailability = snapshot.Carparks.GetValueOrDefault(carpark.CarParkNo);
            IReadOnlyList<CarparkLotAvailability> lots = ToLots(liveAvailability, vehicleType);

            if (request.HasAvailableLots == true && lots.Sum(l => l.AvailableLots) <= 0)
            {
                continue;
            }

            if (vehicleType is not null && lots.Count == 0)
            {
                continue;
            }

            items.Add(ToResult(carpark, snapshot.Timestamp, freshness, lots, distance));
        }

        List<CarparkResult> sorted = items.OrderBy(c => c.DistanceMetres).ThenBy(c => c.CarparkNo).ToList();
        return new CarparkSearchResponse(sorted, sorted.Count, new DataStatus(freshness, snapshot.Timestamp));
    }

    public async Task<CarparkStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, HdbCarpark> carparks = _carparks.Value;
        AvailabilitySnapshot snapshot = await _dataGovSgClient.GetAvailabilityAsync(cancellationToken);
        int matchedCount = carparks.Keys.Count(key => snapshot.Carparks.ContainsKey(key));
        return new CarparkStatusResponse(
            DetermineFreshness(snapshot.Timestamp, snapshot.IsFallback),
            snapshot.Timestamp,
            carparks.Count,
            matchedCount);
    }

    private async Task<CarparkResult?> GetCarparkCoreAsync(string carparkNo, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, HdbCarpark> carparks = _carparks.Value;
        if (!carparks.TryGetValue(carparkNo, out HdbCarpark? carpark))
        {
            return null;
        }

        AvailabilitySnapshot snapshot = await _dataGovSgClient.GetAvailabilityAsync(cancellationToken);
        string freshness = DetermineFreshness(snapshot.Timestamp, snapshot.IsFallback);
        LiveAvailability? liveAvailability = snapshot.Carparks.GetValueOrDefault(carpark.CarParkNo);
        IReadOnlyList<CarparkLotAvailability> lots = ToLots(liveAvailability, vehicleType: null);
        return ToResult(carpark, snapshot.Timestamp, freshness, lots, distanceMetres: null);
    }

    private CarparkResult ToResult(
        HdbCarpark carpark,
        DateTimeOffset? timestamp,
        string freshness,
        IReadOnlyList<CarparkLotAvailability> lots,
        double? distanceMetres)
    {
        return new CarparkResult(
            carpark.CarParkNo,
            carpark.Address,
            carpark.Latitude,
            carpark.Longitude,
            distanceMetres,
            carpark.CarParkType,
            carpark.TypeOfParkingSystem,
            carpark.ShortTermParking,
            carpark.FreeParking,
            carpark.NightParking,
            carpark.CarParkDecks,
            carpark.GantryHeight,
            string.Equals(carpark.CarParkBasement, "Y", StringComparison.OrdinalIgnoreCase),
            new CarparkAvailabilityDetails(timestamp, freshness, lots));
    }

    private IReadOnlyDictionary<string, HdbCarpark> LoadCarparks()
    {
        string csvPath = ResolveCsvPath(_options.CsvPath);
        IReadOnlyList<HdbCarpark> parsed = _csvParser.Parse(csvPath);
        Dictionary<string, HdbCarpark> mapped = new(StringComparer.OrdinalIgnoreCase);

        foreach (HdbCarpark carpark in parsed)
        {
            (double latitude, double longitude) = Svy21Converter.Convert(carpark.XCoord, carpark.YCoord);
            mapped[carpark.CarParkNo] = carpark with { Latitude = latitude, Longitude = longitude };
        }

        _logger.LogInformation("Loaded {Count} HDB carparks from {CsvPath}.", mapped.Count, csvPath);
        return mapped;
    }

    private string ResolveCsvPath(string configuredPath)
    {
        IEnumerable<string> candidates =
        [
            configuredPath,
            Path.Combine(_hostEnvironment.ContentRootPath, configuredPath),
            Path.Combine(AppContext.BaseDirectory, configuredPath),
            Path.Combine(_hostEnvironment.ContentRootPath, "..", "..", "data", Path.GetFileName(configuredPath)),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", Path.GetFileName(configuredPath))
        ];

        foreach (string candidate in candidates.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate HDB CSV data file '{configuredPath}'.");
    }

    private static IReadOnlyList<CarparkLotAvailability> ToLots(LiveAvailability? liveAvailability, string? vehicleType)
    {
        if (liveAvailability?.CarparkInfo is null)
        {
            return [];
        }

        IEnumerable<LiveLotAvailability> source = liveAvailability.CarparkInfo.Where(lot => !string.IsNullOrWhiteSpace(lot.LotType));
        if (vehicleType is not null)
        {
            source = source.Where(lot => string.Equals(lot.LotType, vehicleType, StringComparison.OrdinalIgnoreCase));
        }

        return source
            .Select(lot => new CarparkLotAvailability(
                (lot.LotType ?? string.Empty).ToUpperInvariant(),
                ParseInt(lot.TotalLots),
                ParseInt(lot.LotsAvailable)))
            .OrderBy(lot => lot.LotType)
            .ToList();
    }

    private static int ParseInt(string? value) => int.TryParse(value, out int parsed) ? parsed : 0;

    private static string DetermineFreshness(DateTimeOffset? timestamp, bool isFallback)
    {
        if (timestamp is null)
        {
            return "unavailable";
        }

        TimeSpan age = DateTimeOffset.UtcNow - timestamp.Value.ToUniversalTime();
        if (age <= TimeSpan.FromMinutes(5) && !isFallback)
        {
            return "fresh";
        }

        if (age <= TimeSpan.FromMinutes(30))
        {
            return "stale";
        }

        return "unavailable";
    }

    private static bool IsEnabledFlag(string? value) => string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, "NO", StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeVehicleType(string? vehicleType)
    {
        if (string.IsNullOrWhiteSpace(vehicleType))
        {
            return null;
        }

        return vehicleType.Trim().ToUpperInvariant();
    }
}

public sealed class StaticDataOptions
{
    public string CsvPath { get; set; } = "Data/HDBCarparkInformation.csv";
}
