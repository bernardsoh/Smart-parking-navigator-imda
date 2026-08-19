using CarparkAvailability.ApiApp.Services;

namespace CarparkAvailability.ApiApp.Tests;

public sealed class Svy21ConverterTests
{
    [Fact]
    public void Convert_OriginPoint_ReturnsSurveyOrigin()
    {
        (double latitude, double longitude) = Svy21Converter.Convert(28001.642, 38744.572);

        Assert.InRange(latitude, 1.36665, 1.36668);
        Assert.InRange(longitude, 103.83332, 103.83335);
    }

    [Fact]
    public void Convert_AlbertCentrePoint_ReturnsExpectedWgs84Coordinates()
    {
        (double latitude, double longitude) = Svy21Converter.Convert(30314.7936, 31490.4942);

        Assert.InRange(latitude, 1.3007, 1.3013);
        Assert.InRange(longitude, 103.8540, 103.8546);
    }

    [Fact]
    public void DistanceMetres_RafflesPlaceToBugis_IsReasonable()
    {
        double distance = Svy21Converter.DistanceMetres(1.2837, 103.8518, 1.3010, 103.8556);

        Assert.InRange(distance, 1900, 2100);
    }
}
