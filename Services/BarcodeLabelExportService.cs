using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace AvaloniaApp.Services;

public static class BarcodeLabelExportService
{
    public static void ExportSvg(BarcodeLabelDataResponse label, Stream stream)
    {
        var matrix = Encode(label.Barcode);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        writer.Write(ToSvg(matrix));
        writer.Flush();
    }

    public static void ExportPng(BarcodeLabelDataResponse label, Stream stream)
    {
        const int width = 900;
        const int height = 450;
        var matrix = Encode(label.Barcode);
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var text = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var titleFont = new SKFont(SKTypeface.Default, 38);
        using var priceFont = new SKFont(SKTypeface.Default, 36) { Embolden = true };
        using var numberFont = new SKFont(SKTypeface.Default, 28);
        canvas.DrawText(label.ProductName, 40, 76, SKTextAlign.Left, titleFont, text);
        var price = $"₱{label.RegularPrice:N2}";
        canvas.DrawText(price, width - priceFont.MeasureText(price) - 40, 76, SKTextAlign.Left, priceFont, text);

        const int barcodeWidth = 540;
        const int barcodeHeight = 170;
        var left = (width - barcodeWidth) / 2;
        var top = 96;
        using var bars = new SKPaint { Color = SKColors.Black, IsAntialias = false };
        for (var y = 0; y < matrix.Height; y++)
        for (var x = 0; x < matrix.Width; x++)
            if (matrix[x, y])
                canvas.DrawRect(left + x * barcodeWidth / (float)matrix.Width, top + y * barcodeHeight / (float)matrix.Height,
                    barcodeWidth / (float)matrix.Width + 1, barcodeHeight / (float)matrix.Height + 1, bars);

        var numberWidth = numberFont.MeasureText(label.Barcode);
        canvas.DrawText(label.Barcode, (width - numberWidth) / 2, top + barcodeHeight + 35, SKTextAlign.Left, numberFont, text);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(stream);
    }

    public static void ExportPdf(BarcodeLabelDataResponse label, Stream stream)
    {
        var matrix = Encode(label.Barcode);
        var svg = ToSvg(matrix);

        Document.Create(document => document.Page(page =>
        {
            page.Size(90, 45, Unit.Millimetre);
            page.Margin(4, Unit.Millimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Black));
            page.Content().Column(column =>
            {
                column.Spacing(2);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(label.ProductName).SemiBold().FontSize(11);
                    row.AutoItem().AlignRight().Text($"₱{label.RegularPrice:N2}").Bold().FontSize(12);
                });
                column.Item().AlignCenter().Height(17, Unit.Millimetre).Svg(svg);
                column.Item().AlignCenter().Text(label.Barcode).FontSize(8).LetterSpacing(0.8f);
            });
        })).GeneratePdf(stream);
    }

    internal static string ToSvg(BitMatrix matrix)
    {
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {matrix.Width} {matrix.Height}\" shape-rendering=\"crispEdges\">");
        svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");
        for (var y = 0; y < matrix.Height; y++)
        {
            var x = 0;
            while (x < matrix.Width)
            {
                if (!matrix[x, y]) { x++; continue; }
                var start = x;
                while (x < matrix.Width && matrix[x, y]) x++;
                svg.Append(CultureInfo.InvariantCulture,
                    $"<rect x=\"{start}\" y=\"{y}\" width=\"{x - start}\" height=\"1\" fill=\"black\"/>");
            }
        }
        return svg.Append("</svg>").ToString();
    }

    private static BitMatrix Encode(string barcode)
    {
        var format = barcode.Length == 13 && barcode.All(char.IsDigit)
            ? BarcodeFormat.EAN_13
            : BarcodeFormat.CODE_128;
        return new MultiFormatWriter().encode(
            barcode,
            format,
            600,
            150,
            new Dictionary<EncodeHintType, object>
            {
                [EncodeHintType.MARGIN] = 8,
                [EncodeHintType.PURE_BARCODE] = true
            });
    }
}
