using ClosedXML.Excel;

namespace AvaloniaApp.Services;

public enum ExcelInventoryWorkbookFormat
{
    Legacy,
    StandardTemplate
}

public enum ExcelInventoryIssueSeverity
{
    Warning,
    Error
}

public sealed record ExcelInventoryImportIssue(
    string Code,
    string Message,
    ExcelInventoryIssueSeverity Severity,
    string SourceSheet,
    int? SourceRow = null);

public sealed class ExcelInventorySectionDraft
{
    public required string SourceSheet { get; init; }
    public required int SourceRow { get; init; }
    public required string Heading { get; init; }
    public string? SuggestedSupplierName { get; set; }
    public string? SuggestedCategory { get; set; }
    public ApiInventoryItemType? SuggestedItemType { get; set; }
}

public sealed class ExcelInventoryExcludedSectionDraft
{
    public required string SourceSheet { get; init; }
    public required int SourceRow { get; init; }
    public required string Heading { get; init; }
    public int ProductRowCount { get; set; }
    public string SummaryDisplay => $"{Heading} - {ProductRowCount} product row{(ProductRowCount == 1 ? "" : "s")} skipped";
}

public sealed class ExcelInventorySupplierDraft
{
    public required string SourceSheet { get; init; }
    public required int SourceRow { get; init; }
    public string Name { get; set; } = "";
    public string ContactPerson { get; set; } = "";
    public string Phone { get; set; } = "";
    public List<ExcelInventoryImportIssue> Issues { get; } = [];
}

public sealed class ExcelInventoryPackageDraft
{
    public required string SourceSheet { get; init; }
    public required int SourceRow { get; init; }
    public string ProductSku { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Label { get; set; } = "";
    public int? PiecesPerUnit { get; set; }
    public decimal? RegularPrice { get; set; }
    public decimal? EmployeePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ExcelInventoryImportIssue> Issues { get; } = [];
}

public sealed class ExcelInventoryProductDraft
{
    public required string SourceSheet { get; init; }
    public required int SourceRow { get; init; }
    public ExcelInventorySectionDraft? Section { get; set; }
    public string SupplierName { get; set; } = "";
    public ApiInventoryItemType? ItemType { get; set; }
    public string Sku { get; set; } = "";
    public string PieceBarcode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal? CostPrice { get; set; }
    public decimal? RegularPrice { get; set; }
    public decimal? EmployeePrice { get; set; }
    public int? CriticalReorderLevel { get; set; }
    public int? CriticalOrderQuantity { get; set; }
    public int? WarningReorderLevel { get; set; }
    public int? WarningOrderQuantity { get; set; }
    public int? OpeningDisplayStock { get; set; }
    public int? OpeningBodegaStock { get; set; }
    public List<ExcelInventoryImportIssue> Issues { get; } = [];
}

public sealed class ExcelInventoryImportResult
{
    public required ExcelInventoryWorkbookFormat Format { get; init; }
    public List<ExcelInventorySupplierDraft> Suppliers { get; } = [];
    public List<ExcelInventorySectionDraft> Sections { get; } = [];
    public List<ExcelInventoryExcludedSectionDraft> ExcludedSections { get; } = [];
    public List<ExcelInventoryProductDraft> Products { get; } = [];
    public List<ExcelInventoryPackageDraft> Packages { get; } = [];
    public List<ExcelInventoryImportIssue> Issues { get; } = [];
}

public sealed class ExcelInventoryImportService
{
    private static readonly string[] SupplierHeaders = ["Name", "ContactPerson", "Phone"];
    private static readonly string[] ProductHeaders =
    [
        "SupplierName", "ItemType", "SKU", "PieceBarcode", "Name", "Category", "Unit",
        "CostPrice", "RegularPrice", "EmployeePrice", "CriticalReorderLevel", "CriticalOrderQuantity",
        "WarningReorderLevel", "WarningOrderQuantity", "OpeningDisplayStock", "OpeningBodegaStock"
    ];
    private static readonly string[] PackageHeaders =
    [
        "ProductSKU", "Barcode", "Label", "PiecesPerUnit", "RegularPrice", "EmployeePrice", "IsActive"
    ];

    public ExcelInventoryImportResult Parse(Stream input)
    {
        using var workbook = new XLWorkbook(input);
        if (FindSheet(workbook, "Suppliers") is not null && FindSheet(workbook, "Products") is not null)
            return ParseStandard(workbook);

        foreach (var sheet in workbook.Worksheets)
        {
            var headerRow = FindLegacyHeaderRow(sheet);
            if (headerRow is not null) return ParseLegacy(sheet, headerRow.Value);
        }

        throw new InvalidDataException("The workbook is neither the BPNV standard template nor the supported legacy inventory layout.");
    }

    public void WriteTemplate(Stream output)
    {
        WriteTemplate(output, null);
    }

    public void WriteTemplate(Stream output, ExcelInventoryImportResult? source)
    {
        using var workbook = new XLWorkbook();
        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell("A1").Value = "BPNV Inventory Import Template";
        instructions.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16);
        instructions.Cell("A3").Value = "1. Add suppliers before referencing them from Products.";
        instructions.Cell("A4").Value = "2. Keep SKU and barcode cells formatted as Text so leading zeroes are preserved.";
        instructions.Cell("A5").Value = "3. ItemType must be Merchandise, Consumable, or Supply.";
        instructions.Cell("A6").Value = "4. WarningReorderLevel must be greater than CriticalReorderLevel; order quantities must be positive.";
        instructions.Cell("A7").Value = "5. Packages is optional. ProductSKU links each package to a Products row.";
        instructions.Cell("A8").Value = "6. Opening stocks are base-piece quantities for Display and Bodega.";
        instructions.Column(1).Width = 110;

        var suppliers = workbook.Worksheets.Add("Suppliers");
        WriteHeaders(suppliers, SupplierHeaders);
        var products = workbook.Worksheets.Add("Products");
        WriteHeaders(products, ProductHeaders);
        var packages = workbook.Worksheets.Add("Packages");
        WriteHeaders(packages, PackageHeaders);

        if (source is not null)
        {
            WriteDrafts(suppliers, products, packages, source);
        }

        products.Columns(3, 4).Style.NumberFormat.Format = "@";
        packages.Column(2).Style.NumberFormat.Format = "@";
        StyleTemplateSheet(suppliers, SupplierHeaders.Length);
        StyleTemplateSheet(products, ProductHeaders.Length);
        StyleTemplateSheet(packages, PackageHeaders.Length);
        workbook.SaveAs(output);
    }

    private static void WriteDrafts(
        IXLWorksheet suppliers,
        IXLWorksheet products,
        IXLWorksheet packages,
        ExcelInventoryImportResult source)
    {
        var supplierRows = source.Suppliers
            .Concat(source.Products
                .Where(product => !string.IsNullOrWhiteSpace(product.SupplierName))
                .Select(product => new ExcelInventorySupplierDraft
                {
                    SourceSheet = product.SourceSheet,
                    SourceRow = product.SourceRow,
                    Name = product.SupplierName
                }))
            .Where(supplier => !string.IsNullOrWhiteSpace(supplier.Name))
            .GroupBy(supplier => Normalize(supplier.Name))
            .Select(group => group.First())
            .ToArray();
        for (var index = 0; index < supplierRows.Length; index++)
        {
            var supplier = supplierRows[index];
            WriteRow(suppliers, index + 2, supplier.Name, supplier.ContactPerson, supplier.Phone);
        }

        for (var index = 0; index < source.Products.Count; index++)
        {
            var product = source.Products[index];
            WriteRow(products, index + 2,
                product.SupplierName, product.ItemType?.ToString() ?? "", product.Sku, product.PieceBarcode,
                product.Name, product.Category, product.Unit, product.CostPrice, product.RegularPrice,
                product.EmployeePrice, product.CriticalReorderLevel, product.CriticalOrderQuantity,
                product.WarningReorderLevel, product.WarningOrderQuantity, product.OpeningDisplayStock,
                product.OpeningBodegaStock);
        }

        for (var index = 0; index < source.Packages.Count; index++)
        {
            var package = source.Packages[index];
            WriteRow(packages, index + 2, package.ProductSku, package.Barcode, package.Label,
                package.PiecesPerUnit, package.RegularPrice, package.EmployeePrice, package.IsActive);
        }
    }

    private static void WriteRow(IXLWorksheet sheet, int row, params object?[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var cell = sheet.Cell(row, index + 1);
            switch (values[index])
            {
                case string value: cell.Value = value; break;
                case int value: cell.Value = value; break;
                case decimal value: cell.Value = value; break;
                case bool value: cell.Value = value; break;
            }
        }
    }

    private static ExcelInventoryImportResult ParseLegacy(IXLWorksheet sheet, int headerRow)
    {
        var result = new ExcelInventoryImportResult { Format = ExcelInventoryWorkbookFormat.Legacy };
        var detailHeaderRow = Enumerable.Range(headerRow + 1, 3)
            .FirstOrDefault(row => Normalize(CellText(sheet.Cell(row, 3))) == "ADD" &&
                                   Normalize(CellText(sheet.Cell(row, 4))) == "SALES");
        if (detailHeaderRow == 0) detailHeaderRow = headerRow + 1;

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? detailHeaderRow;
        var lowerHeaderRow = Enumerable.Range(detailHeaderRow + 1, Math.Max(0, lastRow - detailHeaderRow))
            .FirstOrDefault(row => Normalize(CellText(sheet.Cell(row, 1))) == "SALES" &&
                                   Normalize(CellText(sheet.Cell(row, 9))) == "CONSUMABLES");
        var upperLastRow = lowerHeaderRow == 0 ? lastRow : lowerHeaderRow - 1;
        ParseLegacyRows(sheet, detailHeaderRow + 1, upperLastRow, false, result, null);

        if (lowerHeaderRow > 0)
        {
            ParseLegacyRows(sheet, lowerHeaderRow + 1, lastRow, true, result, null);
        }

        return result;
    }

    private static void ParseLegacyRows(
        IXLWorksheet sheet,
        int firstRow,
        int lastRow,
        bool lowerSection,
        ExcelInventoryImportResult result,
        ExcelInventorySectionDraft? initialSection)
    {
        var section = initialSection;
        ExcelInventoryExcludedSectionDraft? excludedSection = null;
        for (var row = firstRow; row <= lastRow; row++)
        {
            var name = CellText(sheet.Cell(row, 1));
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!HasLegacyItemEvidence(sheet, row))
            {
                if (lowerSection && IsLowerHeaderLabel(name)) continue;
                if (!lowerSection && IsExcludedLegacySection(name))
                {
                    excludedSection = new ExcelInventoryExcludedSectionDraft
                    {
                        SourceSheet = sheet.Name,
                        SourceRow = row,
                        Heading = name
                    };
                    result.ExcludedSections.Add(excludedSection);
                    section = null;
                    continue;
                }

                excludedSection = null;
                section = new ExcelInventorySectionDraft
                {
                    SourceSheet = sheet.Name,
                    SourceRow = row,
                    Heading = name,
                    SuggestedSupplierName = name,
                    SuggestedItemType = lowerSection ? ApiInventoryItemType.Consumable : ApiInventoryItemType.Merchandise
                };
                result.Sections.Add(section);
                continue;
            }

            if (excludedSection is not null)
            {
                excludedSection.ProductRowCount++;
                continue;
            }

            var product = new ExcelInventoryProductDraft
            {
                SourceSheet = sheet.Name,
                SourceRow = row,
                Section = section,
                Name = name
            };
            var beginningDisplay = ReadLegacyInteger(sheet.Cell(row, 2), product, "display beginning");
            var displayAdd = ReadLegacyInteger(sheet.Cell(row, 3), product, "display add");
            var sales = ReadLegacyInteger(sheet.Cell(row, 4), product, "sales");
            var accountsReceivable = ReadLegacyInteger(sheet.Cell(row, 5), product, "accounts receivable");
            var spoilage = ReadLegacyInteger(sheet.Cell(row, 6), product, "spoilage/BO");
            var beginningBodega = ReadLegacyInteger(sheet.Cell(row, 9), product, "bodega beginning");
            var bodegaIn = ReadLegacyInteger(sheet.Cell(row, 10), product, "bodega in");
            var bodegaDeduction = lowerSection
                ? ReadLegacyInteger(sheet.Cell(row, 11), product, "bodega out")
                : displayAdd;

            if (lowerSection)
            {
                var suggestedType = SuggestLowerItemType(name, beginningDisplay, displayAdd, sales, accountsReceivable, spoilage);
                if (section?.SuggestedItemType != suggestedType)
                {
                    section = new ExcelInventorySectionDraft
                    {
                        SourceSheet = sheet.Name,
                        SourceRow = row,
                        Heading = suggestedType switch
                        {
                            ApiInventoryItemType.Merchandise => "PREPARED / SELLABLE ITEMS",
                            ApiInventoryItemType.Supply => "OPERATIONAL SUPPLIES",
                            _ => "INTERNAL CONSUMABLES"
                        },
                        SuggestedItemType = suggestedType
                    };
                    result.Sections.Add(section);
                }
                product.Section = section;
                product.ItemType = suggestedType;
            }

            product.OpeningDisplayStock = beginningDisplay + displayAdd - sales - accountsReceivable - spoilage;
            product.OpeningBodegaStock = beginningBodega + bodegaIn - bodegaDeduction;
            ReadLegacyReorderPoint(sheet.Cell(row, 14), product);
            FlagStockIssues(product);
            AddProductIssues(result, product);
            result.Products.Add(product);
        }
    }

    private static ExcelInventoryImportResult ParseStandard(XLWorkbook workbook)
    {
        var result = new ExcelInventoryImportResult { Format = ExcelInventoryWorkbookFormat.StandardTemplate };
        var supplierSheet = FindSheet(workbook, "Suppliers")!;
        var productSheet = FindSheet(workbook, "Products")!;
        var supplierColumns = ReadColumns(supplierSheet, SupplierHeaders);
        var productColumns = ReadColumns(productSheet, ProductHeaders);

        ParseStandardSuppliers(supplierSheet, supplierColumns, result);
        ParseStandardProducts(productSheet, productColumns, result);
        var packageSheet = FindSheet(workbook, "Packages");
        if (packageSheet is not null) ParseStandardPackages(packageSheet, ReadColumns(packageSheet, PackageHeaders), result);
        return result;
    }

    private static void ParseStandardSuppliers(
        IXLWorksheet sheet,
        IReadOnlyDictionary<string, int> columns,
        ExcelInventoryImportResult result)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (RowIsEmpty(sheet, row, columns.Values)) continue;
            var supplier = new ExcelInventorySupplierDraft
            {
                SourceSheet = sheet.Name,
                SourceRow = row,
                Name = Text(sheet, row, columns, "Name"),
                ContactPerson = Text(sheet, row, columns, "ContactPerson"),
                Phone = Text(sheet, row, columns, "Phone")
            };
            if (string.IsNullOrWhiteSpace(supplier.Name)) AddIssue(supplier.Issues, "MissingSupplierName", "Supplier name is required.", sheet, row);
            result.Suppliers.Add(supplier);
            result.Issues.AddRange(supplier.Issues);
        }
    }

    private static void ParseStandardProducts(
        IXLWorksheet sheet,
        IReadOnlyDictionary<string, int> columns,
        ExcelInventoryImportResult result)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (RowIsEmpty(sheet, row, columns.Values)) continue;
            var product = new ExcelInventoryProductDraft
            {
                SourceSheet = sheet.Name,
                SourceRow = row,
                SupplierName = Text(sheet, row, columns, "SupplierName"),
                Sku = Text(sheet, row, columns, "SKU"),
                PieceBarcode = Text(sheet, row, columns, "PieceBarcode", true),
                Name = Text(sheet, row, columns, "Name"),
                Category = Text(sheet, row, columns, "Category"),
                Unit = Text(sheet, row, columns, "Unit"),
                CostPrice = ReadDecimal(sheet, row, columns, "CostPrice", result),
                RegularPrice = ReadDecimal(sheet, row, columns, "RegularPrice", result),
                EmployeePrice = ReadDecimal(sheet, row, columns, "EmployeePrice", result),
                CriticalReorderLevel = ReadInteger(sheet, row, columns, "CriticalReorderLevel", result),
                CriticalOrderQuantity = ReadInteger(sheet, row, columns, "CriticalOrderQuantity", result),
                WarningReorderLevel = ReadInteger(sheet, row, columns, "WarningReorderLevel", result),
                WarningOrderQuantity = ReadInteger(sheet, row, columns, "WarningOrderQuantity", result),
                OpeningDisplayStock = ReadInteger(sheet, row, columns, "OpeningDisplayStock", result),
                OpeningBodegaStock = ReadInteger(sheet, row, columns, "OpeningBodegaStock", result)
            };
            var itemType = Text(sheet, row, columns, "ItemType");
            if (Enum.TryParse<ApiInventoryItemType>(itemType, true, out var parsedType)) product.ItemType = parsedType;
            else AddIssue(product.Issues, "InvalidItemType", "ItemType must be Merchandise, Consumable, or Supply.", sheet, row);

            foreach (var (value, label) in new[]
                     {
                         (product.SupplierName, "SupplierName"), (product.Sku, "SKU"), (product.Name, "Name"),
                         (product.Category, "Category"), (product.Unit, "Unit")
                     })
                if (string.IsNullOrWhiteSpace(value)) AddIssue(product.Issues, "MissingField", $"{label} is required.", sheet, row);

            if (product.CriticalReorderLevel is null || product.WarningReorderLevel is null)
                AddIssue(product.Issues, "MissingReorderPoint", "Critical and warning reorder levels are required.", sheet, row);
            FlagStockIssues(product);
            AddProductIssues(result, product);
            result.Products.Add(product);
        }
    }

    private static void ParseStandardPackages(
        IXLWorksheet sheet,
        IReadOnlyDictionary<string, int> columns,
        ExcelInventoryImportResult result)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (RowIsEmpty(sheet, row, columns.Values)) continue;
            var package = new ExcelInventoryPackageDraft
            {
                SourceSheet = sheet.Name,
                SourceRow = row,
                ProductSku = Text(sheet, row, columns, "ProductSKU"),
                Barcode = Text(sheet, row, columns, "Barcode", true),
                Label = Text(sheet, row, columns, "Label"),
                PiecesPerUnit = ReadInteger(sheet, row, columns, "PiecesPerUnit", result),
                RegularPrice = ReadDecimal(sheet, row, columns, "RegularPrice", result),
                EmployeePrice = ReadDecimal(sheet, row, columns, "EmployeePrice", result)
            };
            var isActive = Text(sheet, row, columns, "IsActive");
            if (bool.TryParse(isActive, out var active)) package.IsActive = active;
            else if (!string.IsNullOrWhiteSpace(isActive)) AddIssue(package.Issues, "InvalidIsActive", "IsActive must be TRUE or FALSE.", sheet, row);

            if (string.IsNullOrWhiteSpace(package.ProductSku) || string.IsNullOrWhiteSpace(package.Barcode) || string.IsNullOrWhiteSpace(package.Label))
                AddIssue(package.Issues, "MissingPackageField", "ProductSKU, Barcode, and Label are required.", sheet, row);
            if (package.PiecesPerUnit is null or <= 1)
                AddIssue(package.Issues, "InvalidPiecesPerUnit", "PiecesPerUnit must be greater than one.", sheet, row);
            result.Packages.Add(package);
            result.Issues.AddRange(package.Issues);
        }
    }

    private static int? FindLegacyHeaderRow(IXLWorksheet sheet)
    {
        foreach (var row in sheet.RowsUsed())
        {
            var values = row.Cells(1, 18).Select(cell => Normalize(CellText(cell))).ToHashSet();
            if (values.Contains("DISPLAYBEGINNINGSTOCK") && values.Contains("DISPLAYENDINGSTOCK") &&
                values.Contains("BODEGABEGINNINGSTOCK") && values.Contains("REORDERPOINT"))
                return row.RowNumber();
        }
        return null;
    }

    private static bool HasLegacyItemEvidence(IXLWorksheet sheet, int row)
    {
        int[] inputColumns = [2, 3, 4, 5, 6, 9, 10, 11, 14];
        if (inputColumns.Any(column => IsNumericInput(sheet.Cell(row, column)))) return true;
        return new[] { 7, 11, 13 }.Any(column => sheet.Cell(row, column).HasFormula);
    }

    private static bool IsNumericInput(IXLCell cell)
    {
        if (cell.HasFormula || cell.IsEmpty()) return false;
        return cell.TryGetValue<decimal>(out _);
    }

    private static bool IsLowerHeaderLabel(string value)
    {
        var normalized = Normalize(value);
        return normalized is "BEGINNING" or "BALANCE" or "END";
    }

    private static bool IsExcludedLegacySection(string value) => Normalize(value) is
        "CHOCOLATECANDIES" or "LUBRICANTS" or "ICECREAMTUBEICE";

    private static int ReadLegacyInteger(IXLCell cell, ExcelInventoryProductDraft product, string field)
    {
        if (cell.HasFormula)
        {
            AddError(product.Issues, "FormulaInputIgnored", $"The legacy {field} input is a formula and cannot be imported safely.", cell.Worksheet, cell.Address.RowNumber);
            return 0;
        }
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(CellText(cell))) return 0;
        if (cell.TryGetValue<decimal>(out var value) && value == decimal.Truncate(value) &&
            value is >= int.MinValue and <= int.MaxValue)
            return decimal.ToInt32(value);
        AddError(product.Issues, "InvalidLegacyNumber", $"The legacy {field} value must be a whole number.", cell.Worksheet, cell.Address.RowNumber);
        return 0;
    }

    private static void ReadLegacyReorderPoint(IXLCell cell, ExcelInventoryProductDraft product)
    {
        if (!cell.HasFormula && cell.TryGetValue<decimal>(out var value) && value > 0 && value == decimal.Truncate(value) && value <= int.MaxValue)
        {
            product.CriticalReorderLevel = 0;
            product.WarningReorderLevel = decimal.ToInt32(value);
            return;
        }
        AddIssue(product.Issues, "MissingReorderPoint", "A positive whole-number legacy reorder point is required.", cell.Worksheet, cell.Address.RowNumber);
    }

    private static IReadOnlyDictionary<string, int> ReadColumns(IXLWorksheet sheet, IReadOnlyList<string> requiredHeaders)
    {
        var columns = sheet.Row(1).CellsUsed().ToDictionary(cell => Normalize(CellText(cell)), cell => cell.Address.ColumnNumber);
        var missing = requiredHeaders.Where(header => !columns.ContainsKey(Normalize(header))).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"Sheet '{sheet.Name}' is missing columns: {string.Join(", ", missing)}.");
        return columns;
    }

    private static string Text(
        IXLWorksheet sheet,
        int row,
        IReadOnlyDictionary<string, int> columns,
        string header,
        bool formatted = false)
    {
        var cell = sheet.Cell(row, columns[Normalize(header)]);
        if (cell.HasFormula) return "";
        return (formatted ? cell.GetFormattedString() : cell.GetString()).Trim();
    }

    private static int? ReadInteger(
        IXLWorksheet sheet,
        int row,
        IReadOnlyDictionary<string, int> columns,
        string header,
        ExcelInventoryImportResult result)
    {
        var cell = sheet.Cell(row, columns[Normalize(header)]);
        if (cell.IsEmpty()) return null;
        if (!cell.HasFormula && cell.TryGetValue<decimal>(out var value) && value == decimal.Truncate(value) &&
            value is >= int.MinValue and <= int.MaxValue)
            return decimal.ToInt32(value);
        result.Issues.Add(new ExcelInventoryImportIssue("InvalidNumber", $"{header} must be a whole number.", ExcelInventoryIssueSeverity.Error, sheet.Name, row));
        return null;
    }

    private static decimal? ReadDecimal(
        IXLWorksheet sheet,
        int row,
        IReadOnlyDictionary<string, int> columns,
        string header,
        ExcelInventoryImportResult result)
    {
        var cell = sheet.Cell(row, columns[Normalize(header)]);
        if (cell.IsEmpty()) return null;
        if (!cell.HasFormula && cell.TryGetValue<decimal>(out var value)) return value;
        result.Issues.Add(new ExcelInventoryImportIssue("InvalidNumber", $"{header} must be numeric.", ExcelInventoryIssueSeverity.Error, sheet.Name, row));
        return null;
    }

    private static void FlagStockIssues(ExcelInventoryProductDraft product)
    {
        if (product.OpeningDisplayStock < 0)
            AddError(product.Issues, "NegativeDisplayStock", "Calculated opening Display stock is negative.", product.SourceSheet, product.SourceRow);
        if (product.OpeningBodegaStock < 0)
            AddError(product.Issues, "NegativeBodegaStock", "Calculated opening Bodega stock is negative.", product.SourceSheet, product.SourceRow);
    }

    private static ApiInventoryItemType SuggestLowerItemType(
        string name,
        int beginningDisplay,
        int displayAdd,
        int sales,
        int accountsReceivable,
        int spoilage)
    {
        var normalized = Normalize(name);
        if (IsOperationalSupply(normalized)) return ApiInventoryItemType.Supply;
        if (beginningDisplay != 0 || displayAdd != 0 || sales != 0 || accountsReceivable != 0 || spoilage != 0)
            return ApiInventoryItemType.Merchandise;

        return IsPreparedMerchandise(normalized)
            ? ApiInventoryItemType.Merchandise
            : ApiInventoryItemType.Consumable;
    }

    private static bool IsPreparedMerchandise(string normalizedName) =>
        normalizedName.Contains("SIOMAI", StringComparison.Ordinal) ||
        normalizedName.Contains("SHARKSFIN", StringComparison.Ordinal) ||
        normalizedName.Contains("HOTDOG", StringComparison.Ordinal) ||
        normalizedName.Contains("SIOPAO", StringComparison.Ordinal) && !normalizedName.Contains("SAUCE", StringComparison.Ordinal) ||
        normalizedName.StartsWith("TJCHICKEN", StringComparison.Ordinal) ||
        normalizedName.StartsWith("TJJUMBO", StringComparison.Ordinal);

    private static bool IsOperationalSupply(string normalizedName) =>
        normalizedName.Contains("DISPOSABLE", StringComparison.Ordinal) ||
        normalizedName.Contains("CUP", StringComparison.Ordinal) ||
        normalizedName.Contains("LID", StringComparison.Ordinal) ||
        normalizedName.Contains("FILTER", StringComparison.Ordinal) ||
        normalizedName.Contains("STIRRER", StringComparison.Ordinal) ||
        normalizedName.Contains("MINIFORK", StringComparison.Ordinal) ||
        normalizedName.Contains("PAPERBOWL", StringComparison.Ordinal) ||
        normalizedName.Contains("HOTDOGBOX", StringComparison.Ordinal) ||
        normalizedName.Contains("PLASTICFOR", StringComparison.Ordinal) ||
        normalizedName.Contains("TISSUE", StringComparison.Ordinal) ||
        normalizedName.Contains("TRASHBAG", StringComparison.Ordinal);

    private static void AddProductIssues(ExcelInventoryImportResult result, ExcelInventoryProductDraft product) =>
        result.Issues.AddRange(product.Issues);

    private static void AddIssue(
        ICollection<ExcelInventoryImportIssue> issues,
        string code,
        string message,
        IXLWorksheet sheet,
        int row) => AddIssue(issues, code, message, sheet.Name, row);

    private static void AddIssue(
        ICollection<ExcelInventoryImportIssue> issues,
        string code,
        string message,
        string sheet,
        int row) => issues.Add(new ExcelInventoryImportIssue(code, message, ExcelInventoryIssueSeverity.Warning, sheet, row));

    private static void AddError(
        ICollection<ExcelInventoryImportIssue> issues,
        string code,
        string message,
        IXLWorksheet sheet,
        int row) => AddError(issues, code, message, sheet.Name, row);

    private static void AddError(
        ICollection<ExcelInventoryImportIssue> issues,
        string code,
        string message,
        string sheet,
        int row) => issues.Add(new ExcelInventoryImportIssue(code, message, ExcelInventoryIssueSeverity.Error, sheet, row));

    private static bool RowIsEmpty(IXLWorksheet sheet, int row, IEnumerable<int> columns) =>
        columns.All(column => sheet.Cell(row, column).IsEmpty());

    private static IXLWorksheet? FindSheet(XLWorkbook workbook, string name) =>
        workbook.Worksheets.FirstOrDefault(sheet => string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string CellText(IXLCell cell) => cell.HasFormula ? "" : cell.GetFormattedString().Trim();

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++) sheet.Cell(1, index + 1).Value = headers[index];
    }

    private static void StyleTemplateSheet(IXLWorksheet sheet, int columnCount)
    {
        var header = sheet.Range(1, 1, 1, columnCount);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F59E0B");
        header.Style.Font.FontColor = XLColor.Black;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, columnCount).AdjustToContents(10, 28);
        sheet.Range(1, 1, 2, columnCount).CreateTable();
    }
}
