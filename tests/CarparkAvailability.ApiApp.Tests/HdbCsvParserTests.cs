using CarparkAvailability.ApiApp.Services;

namespace CarparkAvailability.ApiApp.Tests;

public sealed class HdbCsvParserTests
{
    private readonly HdbCsvParser _parser = new();

    [Fact]
    public void Parse_ValidCsv_ReturnsRows()
    {
        string csv = "car_park_no,address,x_coord,y_coord,car_park_type,type_of_parking_system,short_term_parking,free_parking,night_parking,car_park_decks,gantry_height,car_park_basement\n" +
                     "ACB,ALBERT CENTRE,30314.7936,31490.4942,BASEMENT,ELECTRONIC,WHOLE DAY,NO,YES,1,1.8,Y\n";

        using StringReader reader = new(csv);
        var records = _parser.Parse(reader);

        Assert.Single(records);
        Assert.Equal("ACB", records[0].CarParkNo);
        Assert.Equal(30314.7936, records[0].XCoord, 4);
    }

    [Fact]
    public void Parse_InvalidCsv_ThrowsInvalidDataException()
    {
        string csv = "car_park_no,address,x_coord,y_coord,car_park_type,type_of_parking_system,short_term_parking,free_parking,night_parking,car_park_decks,gantry_height,car_park_basement\n" +
                     "ACB,ALBERT CENTRE,not-a-number,31490.4942,BASEMENT,ELECTRONIC,WHOLE DAY,NO,YES,1,1.8,Y\n";

        using StringReader reader = new(csv);

        Assert.Throws<InvalidDataException>(() => _parser.Parse(reader));
    }
}
