# BPNV Convenience Store

Avalonia desktop prototype for sales and inventory management at **BPNV Convenience Store**.

## Resume Context

The Inventory > Import Excel workflow accepts the supported legacy workbook or the BPNV standard template. It provides section mappings and bulk defaults, performs local and backend validation, and imports suppliers, products, packages, and opening balances only after confirmation.

Inventory > Batch Receive captures Eyoyo keyboard exports as tab-separated text, preserves exact barcode values, validates suppliers and unit conversions with the API, and atomically receives a confirmed batch into Bodega.

Product Catalog supports periodic physical stock counts for active Consumables and Supplies. Inventory users enter the quantity remaining in Display or Bodega; the system records the variance as an attributed stock adjustment and refreshes the catalog balance.

The system deliberately keeps two inventory locations:

- **Display**: sellable stock available at the counter. The internal model still calls this `ShelfStock` for persisted-data compatibility.
- **Bodega**: reserve stock.
- **Total stock** is always calculated as `Display + Bodega`; users never enter it manually.

## Business Rules

- Every inventory item requires a supplier.
- Receiving stock increases Bodega.
- Moving stock to Display deducts Bodega and increases Display by the same quantity, so Total does not change.
- POS sales deduct Display only.
- AR issues deduct Display and are recorded in movement history.
- Spoilage/BO can be recorded against either Display or Bodega.
- Supply usage can be recorded against either location.
- Corrections can increase or decrease either location, subject to available-stock validation.
- Reorder and out-of-stock statuses are calculated from Total stock and the configured reorder level.
- Products at or below Reorder Level use `Target Stock - Total Stock` as the suggested order quantity.
- Stock cannot become negative.

## Inventory Types

The Products page separates inventory into three tabs:

- **Merchandise**: sellable and visible in POS.
- **Consumables**: internally consumed items such as cups, lids, and filters; excluded from POS.
- **Supplies**: operational supplies; excluded from POS.

Excel terminology is generalized into stock movements rather than duplicated as daily worksheet columns.

## Inventory Navigation

Inventory has sidebar children:

- Products
- Suppliers
- Stock Movements
- Batch Receive
- Import Excel

When the sidebar is collapsed, child icons are hidden. Selecting the Inventory icon opens a compact flyout with its destinations. The selected Inventory subsection is restored when the sidebar expands.

Batch Receive follows `Capture -> local parse -> backend validate -> preview -> confirm -> commit`. Scanner rows must contain supplier library, barcode, and positive whole-number quantity separated by true tabs; the raw capture remains available when validation or receipt fails.

## Product Entry

Product catalog, registration, stock receiving, and New Sale use the authenticated MariaDB-backed API. The dedicated **Add Product** inventory page accepts:

- Supplier
- Item type
- Product name
- Critical reorder level and fixed order quantity
- Warning reorder level and fixed order quantity
- Purchase price per piece
- Selling price
- Employee price
- Piece barcode
- Optional package barcodes, piece conversions, and package prices

New products start with zero stock. Use **Receive Stock** to scan a piece or package barcode and receive the converted piece quantity into Bodega. Package prices are suggested from the piece price and remain editable.

SKU, category, and unit are required. Merchandise requires a piece barcode; Consumables and Supplies may leave it blank. Barcode values are stored as text so leading zeroes are preserved.

## Persistence

The application uses a versioned local JSON store:

```text
%LOCALAPPDATA%\BNPV.Stockroom\store.json
```

The technical folder retains `BNPV` for compatibility even though the visible store branding is `BPNV Convenience Store`.

Before each save, the previous document is copied to:

```text
%LOCALAPPDATA%\BNPV.Stockroom\store.json.bak
```

The API database is authoritative for product registration, stock receipts, and sales. The JSON file remains temporarily in use by legacy Overview, Reports, Suppliers, and Stock Movements screens while those screens are migrated to API endpoints. Do not treat its product or sale data as synchronized with MariaDB.

## Prototype Data

An empty first-run store receives prototype data based on examples from the workbook:

- Suppliers such as SHOPPERS, DOUBLE DRAGON, MEGABUCKS, and LUBRICANTS
- Merchandise examples such as Boy Bawang, Wilkins, and lubricant products
- Consumables such as cups, lids, and coffee filters
- Supplies such as tissue and hotdog boxes
- Five sample sales with matching Display deductions and stock-movement records

Prototype seeding never replaces an existing saved inventory. Known older prototype supply records are migrated into the newer Consumable/Supply split.

## Login

Login uses the BPNV backend JWT endpoints. Start MariaDB and `BPNV.Api` before signing in. The API address defaults to `https://localhost:7282/`; set `BPNV_API_BASE_URL` to the server PC address on another LAN client, for example:

```powershell
$env:BPNV_API_BASE_URL = "https://192.168.1.50:7282/"
```

The bootstrap administrator is created from backend configuration when the users table is empty. Production defaults require changing that bootstrap password; local Development can disable the requirement. Access and refresh tokens are kept in memory and cleared on logout.

Navigation follows backend roles: Cashier can access Overview/New Sale, Inventory can access Overview/Inventory/Reports, and Admin can access all current sections. The dashboard opens maximized after login.

## Reports

The Reports page is separated into three tabs:

- **Sales Summary**: sales today, gross sales, transaction count, units sold, top products, and recent sales.
- **Inventory Summary**: Display, Bodega, Total units, selling value, inventory type counts, and current product balances.
- **Order Summary**: supplier-grouped products at or below reorder level with suggested order quantities.

Reports can be exported to PDF and Excel. Inventory reports include:

- Supplier
- Inventory type
- Display, Bodega, and Total balances
- Reorder level and status
- Prices
- Stock movement history

Default export names use `BPNV-store-report`.

## Theme And UI

- Theme source: `Assets/index.css`
- `ThemeConverter` maps CSS tokens into Avalonia resources.
- Text selection, focus outlines, and Fluent control accents use CSS `--primary` and `--ring`.
- Preserve the existing MVVM pattern and keep code-behind limited to view behavior such as sidebar animation and hover flyouts.

## Architecture

- .NET 10
- Avalonia 12.1
- CommunityToolkit.Mvvm
- ClosedXML for Excel report export
- QuestPDF for PDF export
- `StoreState` is the shared application-lifetime state and business-operation service.
- `StorePersistenceService` performs JSON loading, atomic replacement, and backup creation.
- All stock mutations should go through `StoreState` so balances, movement history, persistence, and page refresh events stay consistent.

Important files:

- `StoreData.cs`: products, suppliers, movements, sales, calculations, and store operations
- `Services/StorePersistenceService.cs`: JSON persistence
- `ViewModels/InventoryViewModel.cs`: inventory entry, filtering, and movement commands
- `Views/InventoryView.axaml`: Products, Suppliers, and Stock Movements sections
- `ViewModels/SalesViewModel.cs`: POS workflow
- `Services/ReportExportService.cs`: PDF and Excel exports
- `DashboardWindow.axaml`: sidebar and main navigation
- `Assets/index.css`: theme tokens

## Run And Validate

Run with hot reload:

```powershell
dotnet watch --project .\AvaloniaApp.csproj
```

Release build:

```powershell
dotnet build .\AvaloniaApp.csproj --configuration Release
```

Tests use MSTest 4 with Microsoft.Testing.Platform. Build and run them with:

```powershell
dotnet build .\AvaloniaApp.Tests\AvaloniaApp.Tests.csproj --configuration Release
dotnet run --project .\AvaloniaApp.Tests\AvaloniaApp.Tests.csproj --configuration Release --no-build
```

Using the legacy `dotnet test <project>` form may fail under the installed .NET 10 SDK because this test project uses Microsoft.Testing.Platform.

## Current Validation

At the time this README was created:

- Application Release build: 0 warnings, 0 errors
- Test Release build: 0 warnings, 0 errors
- Tests: 65 passed
