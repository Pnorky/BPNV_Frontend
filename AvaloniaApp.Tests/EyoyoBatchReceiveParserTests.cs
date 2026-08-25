using AvaloniaApp.Services;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class EyoyoBatchReceiveParserTests
{
    private readonly EyoyoBatchReceiveParser _parser = new();

    [TestMethod]
    public void ParsesCrLfAndLfRowsUsingTabsAndPreservesBarcodeText()
    {
        var result = _parser.Parse("Double Dragon Trading\t000012340005\t2\r\nINV\t8992696529168\t20\n");

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(2, result.Records.Count);
        Assert.AreEqual("Double Dragon Trading", result.Records[0].SupplierLibrary);
        Assert.AreEqual("000012340005", result.Records[0].Barcode);
        Assert.AreEqual(2, result.Records[0].UnitQuantity);
        Assert.AreEqual(2, result.Records[1].SourceRecord);
    }

    [TestMethod]
    public void ParsesUnambiguousContinuousTabSequence()
    {
        var result = _parser.Parse("Supplier A\t0001\t3\tSupplier B\t0002\t4");

        Assert.IsTrue(result.IsValid);
        CollectionAssert.AreEqual(new[] { "0001", "0002" }, result.Records.Select(record => record.Barcode).ToArray());
    }

    [TestMethod]
    public void ParsesContinuousTabSequenceEndingInCrLf()
    {
        var result = _parser.Parse("Supplier A\t0001\t3\tSupplier B\t0002\t4\r\n");

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(2, result.Records.Count);
        CollectionAssert.AreEqual(new[] { "Supplier A", "Supplier B" }, result.Records.Select(record => record.SupplierLibrary).ToArray());
        CollectionAssert.AreEqual(new[] { "0001", "0002" }, result.Records.Select(record => record.Barcode).ToArray());
    }

    [TestMethod]
    [DataRow("Supplier A\t0001\t3\tSupplier B\r\n", 2)]
    [DataRow("Supplier A\t0001\t3\tSupplier B\t0002\r\n", 2)]
    [DataRow("Supplier A\t0001\t3\tSupplier B\t0002\t4\t\r\n", 3)]
    public void RejectsIncompleteContinuousSequenceEndingInCrLf(string capture, int sourceRecord)
    {
        var result = _parser.Parse(capture);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == "incompleteRecord" && issue.SourceRecord == sourceRecord));
    }

    [TestMethod]
    public void RejectsScientificNotationAndIncompleteRows()
    {
        var scientific = _parser.Parse("INV\t4.80002E+12\t1");
        var incomplete = _parser.Parse("INV\t0001\t1\tINV\t0002");

        Assert.IsTrue(scientific.Issues.Any(issue => issue.Code == "scientificNotationBarcode"));
        Assert.IsTrue(incomplete.Issues.Any(issue => issue.Code == "incompleteRecord" && issue.SourceRecord == 2));
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("1.5")]
    [DataRow("1E3")]
    [DataRow("2147483648")]
    public void RejectsInvalidQuantities(string quantity)
    {
        var result = _parser.Parse($"INV\t0001\t{quantity}");

        Assert.IsTrue(result.Issues.Any(issue => issue.Code == "invalidQuantity"));
        Assert.AreEqual(0, result.Records.Count);
    }

    [TestMethod]
    public void EnforcesRecordLimit()
    {
        var capture = string.Join('\n', Enumerable.Range(1, EyoyoBatchReceiveParser.MaximumRecords + 1)
            .Select(index => $"INV\t{index:D8}\t1"));

        var result = _parser.Parse(capture);

        Assert.AreEqual(EyoyoBatchReceiveParser.MaximumRecords, result.Records.Count);
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == "batchLimitExceeded"));
    }

    [TestMethod]
    public void AggregatesSameLibraryDuplicatesAndAcceptsDifferentLibrariesForSameBarcode()
    {
        var result = _parser.Parse("Supplier A\t0001\t2\n supplier a \t0001\t3\nSupplier B\t0001\t1");

        Assert.AreEqual(2, result.Aggregates.Count);
        var aggregate = result.Aggregates.Single(item => item.SupplierLibrary.Equals("Supplier A", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(5, aggregate.UnitQuantity);
        CollectionAssert.AreEqual(new[] { 1, 2 }, aggregate.SourceRecords.ToArray());
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(3, result.Records.Count);
        Assert.IsFalse(result.Issues.Any(issue => issue.Code == "conflictingSupplierLibraries"));
    }
}
