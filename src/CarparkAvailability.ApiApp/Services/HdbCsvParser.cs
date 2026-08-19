using System.Globalization;
using CarparkAvailability.ApiApp.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace CarparkAvailability.ApiApp.Services;

public sealed class HdbCsvParser
{
    private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        TrimOptions = TrimOptions.Trim
    };

    public IReadOnlyList<HdbCarpark> Parse(string filePath)
    {
        using StreamReader reader = new(filePath);
        return Parse(reader);
    }

    public IReadOnlyList<HdbCarpark> Parse(TextReader textReader)
    {
        try
        {
            using CsvReader csv = new(textReader, Configuration);
            return csv.GetRecords<HdbCarpark>().ToList();
        }
        catch (Exception exception) when (exception is CsvHelperException or ReaderException)
        {
            throw new InvalidDataException("Failed to parse the HDB carpark CSV data.", exception);
        }
    }
}
