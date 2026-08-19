using CarparkAvailability.ApiApp.Models;
using CarparkAvailability.ApiApp.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.Configure<DataGovSgOptions>(builder.Configuration.GetSection("DataGovSg"));
builder.Services.Configure<StaticDataOptions>(builder.Configuration.GetSection("StaticData"));
builder.Services.AddHttpClient("DataGovSg", (serviceProvider, client) =>
{
    DataGovSgOptions options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataGovSgOptions>>().Value;
    client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddSingleton<HdbCsvParser>();
builder.Services.AddSingleton<DataGovSgClient>();
builder.Services.AddSingleton<IDataGovSgClient>(serviceProvider => serviceProvider.GetRequiredService<DataGovSgClient>());
builder.Services.AddSingleton<CarparkService>();
builder.Services.AddHostedService<DataGovSgRefreshService>();

WebApplication app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api", () => Results.Ok(new
{
    name = "Smart Parking Navigator API",
    status = "ready"
}));

app.MapGet("/api/carparks/nearby", async ([AsParameters] SearchRequest request, CarparkService carparkService, CancellationToken cancellationToken) =>
{
    Dictionary<string, string[]> errors = Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
    }

    CarparkSearchResponse response = await carparkService.SearchNearbyAsync(request, cancellationToken);
    return Results.Ok(response);
});

app.MapGet("/api/carparks/{carparkNo}", async (string carparkNo, CarparkService carparkService, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(carparkNo))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["carparkNo"] = ["Carpark number is required."]
        }, statusCode: StatusCodes.Status400BadRequest);
    }

    CarparkResult? carpark = await carparkService.GetCarparkAsync(carparkNo, cancellationToken);
    return carpark is null ? Results.NotFound() : Results.Ok(carpark);
});

app.MapGet("/api/status", async (CarparkService carparkService, CancellationToken cancellationToken) =>
{
    CarparkStatusResponse status = await carparkService.GetStatusAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapDefaultEndpoints();

app.Run();

static Dictionary<string, string[]> Validate(SearchRequest request)
{
    Dictionary<string, string[]> errors = new(StringComparer.OrdinalIgnoreCase);

    if (request.Lat is null || request.Lat < -90 || request.Lat > 90)
    {
        errors["lat"] = ["lat is required and must be between -90 and 90."];
    }

    if (request.Lon is null || request.Lon < -180 || request.Lon > 180)
    {
        errors["lon"] = ["lon is required and must be between -180 and 180."];
    }

    double radiusMetres = request.RadiusMetres ?? 500;
    if (radiusMetres <= 0 || radiusMetres > 10000)
    {
        errors["radiusMetres"] = ["radiusMetres must be greater than 0 and less than or equal to 10000."];
    }

    if (!string.IsNullOrWhiteSpace(request.VehicleType))
    {
        string normalized = request.VehicleType.Trim().ToUpperInvariant();
        if (normalized is not ("C" or "H" or "Y" or "S"))
        {
            errors["vehicleType"] = ["vehicleType must be one of C, H, Y, or S."];
        }
    }

    return errors;
}

public partial class Program
{
}
