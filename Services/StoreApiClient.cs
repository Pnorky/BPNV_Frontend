using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaloniaApp.Services;

public sealed class StoreApiClient(AuthApiClient authClient)
{
    public bool IsSuperAdmin => authClient.IsSuperAdmin;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<IReadOnlyList<SupplierResponse>> GetSuppliersAsync(
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<SupplierResponse>>(
            () => new HttpRequestMessage(HttpMethod.Get, WithQuery("api/suppliers", ("search", search), ("includeInactive", includeInactive ? "true" : null))),
            cancellationToken);

    public Task<SupplierResponse> CreateSupplierAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<SupplierResponse, CreateSupplierRequest>(HttpMethod.Post, "api/suppliers", request, cancellationToken);

    public Task<SupplierResponse> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken = default) =>
        SendJsonAsync<SupplierResponse, UpdateSupplierRequest>(HttpMethod.Put, $"api/suppliers/{id}", request, cancellationToken);

    public async Task DeactivateSupplierAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await authClient.SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"api/suppliers/{id}"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ReadProblemAsync(response, cancellationToken);
            throw new ApiClientException(response.StatusCode, ProblemMessage(problem, response), problem);
        }
    }

    public async Task ReactivateSupplierAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await authClient.SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"api/suppliers/{id}/reactivate"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ReadProblemAsync(response, cancellationToken);
            throw new ApiClientException(response.StatusCode, ProblemMessage(problem, response), problem);
        }
    }

    public Task<ProductResponse> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<ProductResponse, CreateProductRequest>(HttpMethod.Post, "api/products", request, cancellationToken);

    public Task<PagedResponse<ProductResponse>> GetProductsAsync(
        string? search = null,
        ApiInventoryItemType? itemType = null,
        Guid? supplierId = null,
        bool? sellable = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedResponse<ProductResponse>>(
            () => new HttpRequestMessage(HttpMethod.Get, WithQuery(
                "api/products",
                ("search", search),
                ("itemType", itemType?.ToString()),
                ("supplierId", supplierId?.ToString()),
                ("sellable", sellable?.ToString().ToLowerInvariant()),
                ("page", page.ToString()),
                ("pageSize", pageSize.ToString()))),
            cancellationToken);

    public Task<PagedResponse<PosProductResponse>> GetPosProductsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedResponse<PosProductResponse>>(
            () => new HttpRequestMessage(HttpMethod.Get, WithQuery(
                "api/products/pos",
                ("search", search),
                ("page", page.ToString()),
                ("pageSize", pageSize.ToString()))),
            cancellationToken);

    public Task<PosProductResponse> GetProductByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default) =>
        SendAsync<PosProductResponse>(
            () => new HttpRequestMessage(HttpMethod.Get, $"api/products/by-barcode/{Uri.EscapeDataString(barcode.Trim())}"),
            cancellationToken);

    public Task<PosProductResponse> GetProductForReceivingByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default) =>
        SendAsync<PosProductResponse>(
            () => new HttpRequestMessage(HttpMethod.Get, $"api/products/receiving/by-barcode/{Uri.EscapeDataString(barcode.Trim())}"),
            cancellationToken);

    public Task<StockReceiptResponse> ReceiveStockAsync(
        ReceiveStockRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<StockReceiptResponse, ReceiveStockRequest>(HttpMethod.Post, "api/stock-receipts", request, cancellationToken);

    public Task<BatchReceiptValidationResponse> ValidateBatchReceiptAsync(
        BatchReceiptRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<BatchReceiptValidationResponse, BatchReceiptRequest>(
            HttpMethod.Post, "api/stock-receipts/batch/validate", request, cancellationToken);

    public Task<BatchReceiptResponse> ReceiveBatchAsync(
        BatchReceiptRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<BatchReceiptResponse, BatchReceiptRequest>(
            HttpMethod.Post, "api/stock-receipts/batch", request, cancellationToken);

    public Task<StockTransferResponse> TransferToDisplayAsync(
        TransferStockRequest request, CancellationToken cancellationToken = default) =>
        SendJsonAsync<StockTransferResponse, TransferStockRequest>(HttpMethod.Post, "api/stock-transfers", request, cancellationToken);

    public Task<PagedResponse<StockMovementResponse>> GetStockMovementsAsync(
        string? search = null,
        string? movementType = null,
        string? reference = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtcExclusive = null,
        int page = 1,
        int pageSize = 20,
        string sortBy = "occurredAt",
        string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedResponse<StockMovementResponse>>(
            () => new HttpRequestMessage(HttpMethod.Get, WithQuery(
                "api/stock-movements",
                ("search", search),
                ("movementType", movementType),
                ("reference", reference),
                ("fromUtc", fromUtc?.ToString("O")),
                ("toUtcExclusive", toUtcExclusive?.ToString("O")),
                ("sortBy", sortBy),
                ("sortDirection", sortDirection),
                ("page", page.ToString()),
                ("pageSize", pageSize.ToString()))),
            cancellationToken);

    public Task<SaleResponse> CreateSaleAsync(
        CreateSaleRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<SaleResponse, CreateSaleRequest>(HttpMethod.Post, "api/sales", request, cancellationToken);

    public Task<InventoryImportValidationResult> ValidateInventoryImportAsync(
        InventoryImportRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<InventoryImportValidationResult, InventoryImportRequest>(
            HttpMethod.Post, "api/inventory-imports/validate", request, cancellationToken);

    public async Task<InventoryImportCommitResult> ImportInventoryAsync(
        InventoryImportRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await authClient.SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "api/inventory-imports")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            },
            cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return await response.Content.ReadFromJsonAsync<InventoryImportCommitResult>(JsonOptions, cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "The API returned an empty response.");

        var problem = await ReadProblemAsync(response, cancellationToken);
        throw new ApiClientException(response.StatusCode, ProblemMessage(problem, response), problem);
    }

    private Task<TResponse> SendJsonAsync<TResponse, TRequest>(
        HttpMethod method,
        string uri,
        TRequest body,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(
            () => new HttpRequestMessage(method, uri)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            cancellationToken);

    private async Task<T> SendAsync<T>(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        using var response = await authClient.SendAuthorizedAsync(requestFactory, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ReadProblemAsync(response, cancellationToken);
            throw new ApiClientException(response.StatusCode, ProblemMessage(problem, response), problem);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new ApiClientException(response.StatusCode, "The API returned an empty response.");
    }

    private static async Task<ApiProblemDetails?> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiProblemDetails>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ProblemMessage(ApiProblemDetails? problem, HttpResponseMessage response)
    {
        if (!string.IsNullOrWhiteSpace(problem?.Detail)) return problem.Detail;
        if (problem?.Errors is { Count: > 0 })
            return string.Join(" ", problem.Errors.Values.SelectMany(messages => messages));
        return problem?.Title ?? $"The API returned {(int)response.StatusCode} {response.ReasonPhrase}.";
    }

    private static string WithQuery(string path, params (string Name, string? Value)[] values)
    {
        var query = string.Join("&", values
            .Where(value => !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => $"{Uri.EscapeDataString(value.Name)}={Uri.EscapeDataString(value.Value!)}"));
        return query.Length == 0 ? path : $"{path}?{query}";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
