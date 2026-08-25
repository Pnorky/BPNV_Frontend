using System.Globalization;
using System.Text.RegularExpressions;

namespace AvaloniaApp.Services;

public sealed record EyoyoBatchReceiveParseIssue(
    string Code,
    string Field,
    int? SourceRecord,
    string Message);

public sealed record EyoyoBatchReceiveAggregate(
    IReadOnlyList<int> SourceRecords,
    string SupplierLibrary,
    string Barcode,
    int UnitQuantity);

public sealed record EyoyoBatchReceiveParseResult(
    IReadOnlyList<BatchReceiptRecordRequest> Records,
    IReadOnlyList<EyoyoBatchReceiveAggregate> Aggregates,
    IReadOnlyList<EyoyoBatchReceiveParseIssue> Issues)
{
    public bool IsValid => Records.Count > 0 && Issues.Count == 0;
}

public sealed partial class EyoyoBatchReceiveParser
{
    public const int MaximumRecords = 1_000;
    public const int MaximumSupplierLibraryLength = 160;
    public const int MaximumBarcodeLength = 64;

    public EyoyoBatchReceiveParseResult Parse(string? capture)
    {
        var records = new List<BatchReceiptRecordRequest>();
        var issues = new List<EyoyoBatchReceiveParseIssue>();
        var value = capture ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Issue("required", "records", null, "Paste or scan at least one receipt record."));
            return new([], [], issues);
        }

        var rows = SplitRows(value, issues);
        if (rows.Count > MaximumRecords)
            issues.Add(Issue("batchLimitExceeded", "records", null, $"A batch cannot exceed {MaximumRecords:N0} records."));

        for (var index = 0; index < Math.Min(rows.Count, MaximumRecords); index++)
            ParseRow(rows[index], index + 1, records, issues);

        var aggregates = Aggregate(records, issues);
        return new(records, aggregates, issues);
    }

    private static List<string[]> SplitRows(string capture, ICollection<EyoyoBatchReceiveParseIssue> issues)
    {
        var content = capture.TrimEnd('\r', '\n');
        if (content.IndexOfAny(['\r', '\n']) < 0)
        {
            var tokens = content.Split('\t', StringSplitOptions.None);
            if (tokens.Length % 3 != 0)
            {
                issues.Add(Issue("incompleteRecord", "records", tokens.Length / 3 + 1,
                    "The continuous export ends with an incomplete record; expected supplier library, barcode, and quantity."));
                return [];
            }

            return tokens.Chunk(3).Select(chunk => chunk.ToArray()).ToList();
        }

        var rows = new List<string[]>();
        foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            if (line.Length == 0) continue;
            rows.Add(line.Split('\t', StringSplitOptions.None));
        }
        return rows;
    }

    private static void ParseRow(
        string[] columns,
        int sourceRecord,
        ICollection<BatchReceiptRecordRequest> records,
        ICollection<EyoyoBatchReceiveParseIssue> issues)
    {
        if (columns.Length != 3)
        {
            issues.Add(Issue("invalidColumnCount", "records", sourceRecord,
                $"Record {sourceRecord} must contain exactly three tab-separated columns: supplier library, barcode, and quantity."));
            return;
        }

        var library = columns[0].Trim();
        var barcode = columns[1].Trim();
        var quantityText = columns[2].Trim();
        var quantity = 0;
        var valid = true;

        if (library.Length == 0 || library.Length > MaximumSupplierLibraryLength)
        {
            issues.Add(Issue("invalidSupplierLibrary", "supplierLibrary", sourceRecord,
                $"Record {sourceRecord}: supplier library is required and must not exceed {MaximumSupplierLibraryLength} characters."));
            valid = false;
        }

        if (barcode.Length == 0 || barcode.Length > MaximumBarcodeLength)
        {
            issues.Add(Issue("invalidBarcode", "barcode", sourceRecord,
                $"Record {sourceRecord}: barcode is required and must not exceed {MaximumBarcodeLength} characters."));
            valid = false;
        }
        else if (ScientificNotationBarcode().IsMatch(barcode))
        {
            issues.Add(Issue("scientificNotationBarcode", "barcode", sourceRecord,
                $"Record {sourceRecord}: scientific-notation barcodes are not accepted because the original barcode cannot be recovered."));
            valid = false;
        }

        if (!PositiveWholeNumber().IsMatch(quantityText) ||
            !int.TryParse(quantityText, NumberStyles.None, CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
        {
            issues.Add(Issue("invalidQuantity", "unitQuantity", sourceRecord,
                $"Record {sourceRecord}: quantity must be a positive whole number no greater than {int.MaxValue:N0}."));
            valid = false;
        }

        if (valid) records.Add(new BatchReceiptRecordRequest(sourceRecord, library, barcode, quantity));
    }

    private static IReadOnlyList<EyoyoBatchReceiveAggregate> Aggregate(
        IReadOnlyList<BatchReceiptRecordRequest> records,
        ICollection<EyoyoBatchReceiveParseIssue> issues)
    {
        var aggregates = new Dictionary<(string Library, string Barcode), MutableAggregate>(new BatchKeyComparer());
        foreach (var record in records)
        {
            var key = (record.SupplierLibrary, record.Barcode);
            if (!aggregates.TryGetValue(key, out var aggregate))
            {
                aggregates.Add(key, new MutableAggregate(record.SupplierLibrary, record.Barcode, record.UnitQuantity, [record.SourceRecord]));
                continue;
            }

            try
            {
                aggregate.UnitQuantity = checked(aggregate.UnitQuantity + record.UnitQuantity);
                aggregate.SourceRecords.Add(record.SourceRecord);
            }
            catch (OverflowException)
            {
                aggregate.HasOverflow = true;
                issues.Add(Issue("duplicateQuantityOverflow", "unitQuantity", record.SourceRecord,
                    $"Record {record.SourceRecord}: combined quantity for this supplier library and barcode is too large."));
            }
        }

        return aggregates.Values.Where(item => !item.HasOverflow)
            .Select(item => new EyoyoBatchReceiveAggregate(item.SourceRecords.ToArray(), item.Library, item.Barcode, item.UnitQuantity))
            .ToArray();
    }

    private static EyoyoBatchReceiveParseIssue Issue(string code, string field, int? sourceRecord, string message) =>
        new(code, field, sourceRecord, message);

    private sealed class MutableAggregate(string library, string barcode, int unitQuantity, List<int> sourceRecords)
    {
        public string Library { get; } = library;
        public string Barcode { get; } = barcode;
        public int UnitQuantity { get; set; } = unitQuantity;
        public List<int> SourceRecords { get; } = sourceRecords;
        public bool HasOverflow { get; set; }
    }

    private sealed class BatchKeyComparer : IEqualityComparer<(string Library, string Barcode)>
    {
        public bool Equals((string Library, string Barcode) x, (string Library, string Barcode) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Library, y.Library) && StringComparer.Ordinal.Equals(x.Barcode, y.Barcode);

        public int GetHashCode((string Library, string Barcode) value) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Library), StringComparer.Ordinal.GetHashCode(value.Barcode));
    }

    [GeneratedRegex(@"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)[eE][+-]?\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex ScientificNotationBarcode();

    [GeneratedRegex(@"^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PositiveWholeNumber();
}
