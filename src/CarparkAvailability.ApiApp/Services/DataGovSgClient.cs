using System.Net.Http.Json;
using CarparkAvailability.ApiApp.Models;
using Microsoft.Extensions.Options;

namespace CarparkAvailability.ApiApp.Services;

public interface IDataGovSgClient
{
    Task<AvailabilitySnapshot> GetAvailabilityAsync(CancellationToken cancellationToken = default);
    Task<AvailabilitySnapshot> RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class DataGovSgClient : IDataGovSgClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DataGovSgClient> _logger;
    private readonly DataGovSgOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private AvailabilitySnapshot? _cachedSnapshot;

    public DataGovSgClient(
        IHttpClientFactory httpClientFactory,
        IOptions<DataGovSgOptions> options,
        ILogger<DataGovSgClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<AvailabilitySnapshot> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSnapshot is not null)
        {
            return _cachedSnapshot;
        }

        return await RefreshAsync(cancellationToken);
    }

    public async Task<AvailabilitySnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            using HttpClient client = _httpClientFactory.CreateClient("DataGovSg");
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                client.DefaultRequestHeaders.Remove("x-api-key");
                client.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
            }

            DataGovSgAvailabilityResponse? response = await client.GetFromJsonAsync<DataGovSgAvailabilityResponse>(
                "transport/carpark-availability",
                cancellationToken);

            AvailabilitySnapshot snapshot = BuildSnapshot(response, isFallback: false);
            _cachedSnapshot = snapshot;
            return snapshot;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to refresh data.gov.sg carpark availability.");
            if (_cachedSnapshot is not null)
            {
                _cachedSnapshot = _cachedSnapshot with { IsFallback = true };
                return _cachedSnapshot;
            }

            return AvailabilitySnapshot.Empty;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static AvailabilitySnapshot BuildSnapshot(DataGovSgAvailabilityResponse? response, bool isFallback)
    {
        AvailabilityItem? item = response?.Items?.FirstOrDefault();
        Dictionary<string, LiveAvailability> carparks = new(StringComparer.OrdinalIgnoreCase);

        if (item?.CarparkData is not null)
        {
            foreach (LiveAvailability carpark in item.CarparkData)
            {
                if (!string.IsNullOrWhiteSpace(carpark.CarparkNumber))
                {
                    carparks[carpark.CarparkNumber] = carpark;
                }
            }
        }

        return new AvailabilitySnapshot(item?.Timestamp, carparks, isFallback, response?.ApiInfo?.Status);
    }
}

public sealed class DataGovSgRefreshService : BackgroundService
{
    private readonly IDataGovSgClient _dataGovSgClient;
    private readonly ILogger<DataGovSgRefreshService> _logger;

    public DataGovSgRefreshService(IDataGovSgClient dataGovSgClient, ILogger<DataGovSgRefreshService> logger)
    {
        _dataGovSgClient = dataGovSgClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _dataGovSgClient.RefreshAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Background refresh for carpark availability failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}

public sealed class DataGovSgOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.data.gov.sg/v1";
}
