using System.Text;
using AvaloniaApp.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QuestPDF.Infrastructure;
using ZXing.Common;
using QuestSettings = QuestPDF.Settings;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class BarcodeLabelExportServiceTests
{
    [TestMethod]
    public void ExportPdfCreatesPrintableCode128Label()
    {
        QuestSettings.License = LicenseType.Community;
        var label = new BarcodeLabelDataResponse(
            Guid.NewGuid(), Guid.NewGuid(), "Coffee", "cup", "COF-001", "BPNV-0123456789ABCDEF", 45m);
        using var stream = new MemoryStream();

        BarcodeLabelExportService.ExportPdf(label, stream);

        Assert.IsTrue(stream.Length > 1_000);
        Assert.AreEqual("%PDF", Encoding.ASCII.GetString(stream.ToArray(), 0, 4));
    }

    [TestMethod]
    public void ToSvgPreservesMatrixDimensionsAndDarkModules()
    {
        var matrix = new BitMatrix(3, 2);
        matrix[1, 0] = true;

        var svg = BarcodeLabelExportService.ToSvg(matrix);

        StringAssert.Contains(svg, "viewBox=\"0 0 3 2\"");
        StringAssert.Contains(svg, "<rect x=\"1\" y=\"0\" width=\"1\" height=\"1\" fill=\"black\"/>");
    }
}
