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
    int UnitQuantity,
    string? LotCode = null,
    DateTimeOffset? ExpiresAtUtc = null);

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
            var width = ContinuousRecordWidth(tokens);
            if (width == 0)
            {
                issues.Add(Issue("incompleteRecord", "records", tokens.Length / 3 + 1,
                    "The continuous export ends with an incomplete record; expected supplier library, barcode, quantity, optional lot, and optional expiry."));
                return [];
            }

            return tokens.Chunk(width).Select(chunk => chunk.ToArray()).ToList();
        }

        var rows = new List<string[]>();
        foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            if (line.Length == 0) continue;
            rows.Add(line.Split('\t', StringSplitOptions.None));
        }
        return rows;
    }

    private static int ContinuousRecordWidth(string[] tokens)
    {
        if (tokens.Length % 5 == 0)
        {
            var valid = true;
            for (var index = 0; index < tokens.Length; index += 5)
            {
                if (!PositiveWholeNumber().IsMatch(tokens[index + 2].Trim())) { valid = false; break; }
                if (!string.IsNullOrWhiteSpace(tokens[index + 4]) &&
                    !TryParseStoreExpiry(tokens[index + 4].Trim(), out _)) { valid = false; break; }
            }
            if (valid) return 5;
        }
        return tokens.Length % 3 == 0 ? 3 : 0;
    }

    private static void ParseRow(
        string[] columns,
        int sourceRecord,
        ICollection<BatchReceiptRecordRequest> records,
        ICollection<EyoyoBatchReceiveParseIssue> issues)
    {
            if (columns.Length is < 3 or > 5)
            {
                issues.Add(Issue("invalidColumnCount", "records", sourceRecord,
                $"Record {sourceRecord} must contain three to five tab-separated columns: supplier library, barcode, quantity, optional lot, and optional expiry."));
                return;
            }

        var library = columns[0].Trim();
        var barcode = columns[1].Trim();
        var quantityText = columns[2].Trim();
        var quantity = 0;
        var lotCode = columns.Length >= 4 ? NullIfWhiteSpace(columns[3]) : null;
        DateTimeOffset? expiresAtUtc = null;
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

        if (lotCode?.Length > 100)
        {
            issues.Add(Issue("invalidLotCode", "lotCode", sourceRecord,
                $"Record {sourceRecord}: lot code must not exceed 100 characters."));
            valid = false;
        }

        if (columns.Length == 5 && !string.IsNullOrWhiteSpace(columns[4]))
        {
            if (!TryParseStoreExpiry(columns[4].Trim(), out expiresAtUtc))
            {
                issues.Add(Issue("invalidExpiry", "expiresAtUtc", sourceRecord,
                    $"Record {sourceRecord}: expiry must use yyyy-MM-dd or yyyy-MM-dd HH:mm."));
                valid = false;
            }
        }

        if (valid) records.Add(new BatchReceiptRecordRequest(
            sourceRecord, library, barcode, quantity, lotCode, ExpiresAtUtc: expiresAtUtc));
    }

    private static IReadOnlyList<EyoyoBatchReceiveAggregate> Aggregate(
        IReadOnlyList<BatchReceiptRecordRequest> records,
        ICollection<EyoyoBatchReceiveParseIssue> issues)
    {
        var aggregates = new Dictionary<(string Library, string Barcode, string LotCode, long? ExpiresTicks), MutableAggregate>(new BatchKeyComparer());
        foreach (var record in records)
        {
            var key = (record.SupplierLibrary!, record.Barcode!, record.LotCode ?? "", record.ExpiresAtUtc?.UtcTicks);
            if (!aggregates.TryGetValue(key, out var aggregate))
            {
                aggregates.Add(key, new MutableAggregate(record.SupplierLibrary!, record.Barcode!, record.UnitQuantity,
                    [record.SourceRecord], record.LotCode, record.ExpiresAtUtc));
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
            .Select(item => new EyoyoBatchReceiveAggregate(item.SourceRecords.ToArray(), item.Library, item.Barcode,
                item.UnitQuantity, item.LotCode, item.ExpiresAtUtc))
            .ToArray();
    }

    private static EyoyoBatchReceiveParseIssue Issue(string code, string field, int? sourceRecord, string message) =>
        new(code, field, sourceRecord, message);

    private static bool TryParseStoreExpiry(string value, out DateTimeOffset? expiresAtUtc)
    {
        expiresAtUtc = null;
        var formats = new[] { "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm" };
        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local)) return false;
        if (value.Length == 10) local = local.Date.AddDays(1).AddTicks(-1);
        expiresAtUtc = StoreDateTime.CombineStoreDateAndTimeToUtc(
            StoreDateTime.AtStoreMidnight(local), local.TimeOfDay);
        return true;
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class MutableAggregate(
        string library, string barcode, int unitQuantity, List<int> sourceRecords,
        string? lotCode, DateTimeOffset? expiresAtUtc)
    {
        public string Library { get; } = library;
        public string Barcode { get; } = barcode;
        public int UnitQuantity { get; set; } = unitQuantity;
        public List<int> SourceRecords { get; } = sourceRecords;
        public bool HasOverflow { get; set; }
        public string? LotCode { get; } = lotCode;
        public DateTimeOffset? ExpiresAtUtc { get; } = expiresAtUtc;
    }

    private sealed class BatchKeyComparer : IEqualityComparer<(string Library, string Barcode, string LotCode, long? ExpiresTicks)>
    {
        public bool Equals(
            (string Library, string Barcode, string LotCode, long? ExpiresTicks) x,
            (string Library, string Barcode, string LotCode, long? ExpiresTicks) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Library, y.Library) &&
            StringComparer.Ordinal.Equals(x.Barcode, y.Barcode) &&
            StringComparer.Ordinal.Equals(x.LotCode, y.LotCode) && x.ExpiresTicks == y.ExpiresTicks;

        public int GetHashCode((string Library, string Barcode, string LotCode, long? ExpiresTicks) value) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Library),
                StringComparer.Ordinal.GetHashCode(value.Barcode), StringComparer.Ordinal.GetHashCode(value.LotCode), value.ExpiresTicks);
    }

    [GeneratedRegex(@"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)[eE][+-]?\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex ScientificNotationBarcode();

    [GeneratedRegex(@"^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PositiveWholeNumber();
}
