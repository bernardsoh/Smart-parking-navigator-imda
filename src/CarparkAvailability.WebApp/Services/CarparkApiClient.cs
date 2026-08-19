using System.Net.Http.Json;
using System.Text;
using CarparkAvailability.WebApp.Models;

namespace CarparkAvailability.WebApp.Services;

public sealed class CarparkApiClient
{
    private readonly HttpClient _httpClient;

    public CarparkApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CarparkSearchResponse> SearchNearbyAsync(CarparkSearchQuery query, CancellationToken cancellationToken = default)
    {
        StringBuilder builder = new("/api/carparks/nearby?");
        builder.Append(FormattableString.Invariant(
            $"lat={query.Lat:0.######}&lon={query.Lon:0.######}&radiusMetres={query.RadiusMetres:0}"));

        if (!string.IsNullOrWhiteSpace(query.VehicleType))
        {
            builder.Append($"&vehicleType={Uri.EscapeDataString(query.VehicleType)}");
        }

        if (query.HasAvailableLots)
        {
            builder.Append("&hasAvailableLots=true");
        }

        if (query.HasFreeParking)
        {
            builder.Append("&hasFreeParking=true");
        }

        if (query.HasNightParking)
        {
            builder.Append("&hasNightParking=true");
        }

        CarparkSearchResponse? response = await _httpClient.GetFromJsonAsync<CarparkSearchResponse>(builder.ToString(), cancellationToken);
        return response ?? new CarparkSearchResponse([], 0, new DataStatus("unavailable", null));
    }

    public async Task<CarparkStatusResponse?> GetStatusAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<CarparkStatusResponse>("/api/status", cancellationToken);
}
