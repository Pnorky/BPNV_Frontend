# BPNV Client Installer

This WiX v4 project creates a per-machine MSI for the self-contained Windows frontend.

## Prerequisites

- .NET 10 SDK
- WiX Toolset SDK restore access
- Windows x64

## Build

From the frontend directory, publish the application first:

```powershell
dotnet publish .\AvaloniaApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64
```

Then build the MSI from this directory:

```powershell
dotnet build .\BPNV.Client.wixproj -c Release
```

The build publishes the frontend to `..\publish\win-x64` and writes the MSI under `bin\Release`.

The installer includes the frontend only. MariaDB and the BPNV API are installed separately on the server PC.

The current installer version is defined in `BPNV.Client.wixproj`:

```xml
<InstallerVersion>1.0.0</InstallerVersion>
```

Update that value for each release. The MSI filename will include the version, for example `BPNV-Client-1.0.0.msi`.
