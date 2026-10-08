using CsvHelper;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Api.Analysis;

public static class ReferenceDataLoader
{
    public static ReferenceData Load(string dataDir)
    {
        var payerRules = LoadPayerRules(dataDir);
        var reasonCodes = ReadCsv(dataDir, "carc_rarc_reference.csv", ReasonCodeReader.Read);
        var labels = ReadCsv(dataDir, "labeled_denials_sample.csv", LabeledDenialsReader.Read);
        var policies = PolicyLibrary.Parse(PolicyFiles(dataDir).Select(path => (Path.GetFileName(path), File.ReadAllText(path))));
        return new ReferenceData(payerRules, policies, reasonCodes, labels);
    }

    public static IReadOnlyDictionary<string, PayerRule> LoadPayerRules(string dataDir) => ReadCsv(dataDir, "payer_rules.csv", PayerRulesReader.Read);

    public static IEnumerable<string> PolicyFiles(string dataDir) =>
        Directory.GetFiles(Path.Combine(dataDir, "payer_policies"), "*.md").Order(StringComparer.Ordinal);

    // CsvHelper messages can echo raw row data, so only the file name is surfaced.
    private static T ReadCsv<T>(string dataDir, string fileName, Func<TextReader, T> read)
    {
        using var reader = new StreamReader(Path.Combine(dataDir, fileName));
        try
        {
            return read(reader);
        }
        catch (CsvHelperException)
        {
            throw new InvalidDataException($"{fileName} could not be read.");
        }
    }
}
