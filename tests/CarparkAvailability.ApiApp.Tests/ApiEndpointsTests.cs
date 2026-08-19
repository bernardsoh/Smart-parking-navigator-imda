using CarparkAvailability.ApiApp.Models;
using CarparkAvailability.ApiApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;

namespace CarparkAvailability.ApiApp.Tests;

public sealed class ApiEndpointsTests : IClassFixture<ApiAppFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointsTests(ApiAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Nearby_WithInvalidLatitude_ReturnsBadRequest()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/carparks/nearby?lat=200&lon=103.85");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Nearby_WithValidRequest_ReturnsResults()
    {
        CarparkSearchResponse? response = await _client.GetFromJsonAsync<CarparkSearchResponse>("/api/carparks/nearby?lat=1.3010&lon=103.8556&radiusMetres=800&hasAvailableLots=true&vehicleType=C");

        Assert.NotNull(response);
        Assert.True(response.Total > 0);
        Assert.All(response.Items, item => Assert.NotNull(item.Availability));
    }

    [Fact]
    public async Task GetCarpark_WhenMissing_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/carparks/UNKNOWN123");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Status_ReturnsAggregateCounts()
    {
        CarparkStatusResponse? response = await _client.GetFromJsonAsync<CarparkStatusResponse>("/api/status");

        Assert.NotNull(response);
        Assert.True(response.TotalCarparks > 1000);
        Assert.True(response.MatchedCarparks >= 1);
    }
}

public sealed class ApiAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        string csvPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../data/HDBCarparkInformation.csv"));

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StaticData:CsvPath"] = csvPath
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDataGovSgClient>();
            services.AddSingleton<IDataGovSgClient>(new FakeDataGovSgClient());
        });
    }
}

public sealed class FakeDataGovSgClient : IDataGovSgClient
{
    private readonly AvailabilitySnapshot _snapshot = new(
        DateTimeOffset.UtcNow,
        new Dictionary<string, LiveAvailability>(StringComparer.OrdinalIgnoreCase)
        {
            ["ACB"] = new LiveAvailability(
                "ACB",
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                [
                    new LiveLotAvailability("500", "C", "123"),
                    new LiveLotAvailability("100", "H", "12")
                ])
        },
        false,
        "healthy");

    public Task<AvailabilitySnapshot> GetAvailabilityAsync(CancellationToken cancellationToken = default) => Task.FromResult(_snapshot);

    public Task<AvailabilitySnapshot> RefreshAsync(CancellationToken cancellationToken = default) => Task.FromResult(_snapshot);
}
