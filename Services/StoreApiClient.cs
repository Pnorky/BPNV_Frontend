using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaloniaApp.Services;

public sealed class StoreApiClient(AuthApiClient authClient)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<IReadOnlyList<SupplierResponse>> GetSuppliersAsync(
        string? search = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<SupplierResponse>>(
            () => new HttpRequestMessage(HttpMethod.Get, WithQuery("api/suppliers", ("search", search))),
            cancellationToken);

    public Task<SupplierResponse> CreateSupplierAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<SupplierResponse, CreateSupplierRequest>(HttpMethod.Post, "api/suppliers", request, cancellationToken);

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

    public Task<SaleResponse> CreateSaleAsync(
        CreateSaleRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<SaleResponse, CreateSaleRequest>(HttpMethod.Post, "api/sales", request, cancellationToken);

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
