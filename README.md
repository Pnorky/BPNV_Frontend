# BPNV Convenience Store

Avalonia desktop prototype for sales and inventory management at **BPNV Convenience Store**.

## Resume Context

This project originally referenced the store's Excel inventory workbook, but it does **not import Excel files**. The workbook was used only to understand the business process. Staff enter suppliers, products, opening balances, and later stock movements directly in the application.

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

When the sidebar is collapsed, child icons are hidden. Hovering the Inventory icon opens a compact flyout with the three destinations. The selected Inventory subsection is restored when the sidebar expands.

## Product Entry

The Add Product panel is collapsed by default. Primary inputs are:

- Supplier
- Item type
- Product name
- Reorder level
- Target stock level
- Regular price
- Employee price
- Opening Display stock
- Opening Bodega stock

The Products toolbar includes an **Order Summary** button. It groups low-stock products by supplier and shows On Hand, Reorder Level, Target Stock, and the suggested quantity to order. Products without a target stock level are omitted.

SKU is generated when omitted. Category defaults to `Uncategorized`, unit defaults to `pcs`, and cost defaults to zero in the simplified form.

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

Products, suppliers, sales, and stock movements survive logout and application restarts. Do not delete this file unless intentionally resetting local data.

## Prototype Data

An empty first-run store receives prototype data based on examples from the workbook:

- Suppliers such as SHOPPERS, DOUBLE DRAGON, MEGABUCKS, and LUBRICANTS
- Merchandise examples such as Boy Bawang, Wilkins, and lubricant products
- Consumables such as cups, lids, and coffee filters
- Supplies such as tissue and hotdog boxes
- Five sample sales with matching Display deductions and stock-movement records

Prototype seeding never replaces an existing saved inventory. Known older prototype supply records are migrated into the newer Consumable/Supply split.

## Login

Prototype credentials:

```text
Username: admin
Password: password123
```

The dashboard opens maximized after login.

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
- Tests: 8 passed
