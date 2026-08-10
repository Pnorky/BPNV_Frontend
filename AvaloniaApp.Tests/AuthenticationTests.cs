using System.Net;
using System.Net.Http.Json;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class AuthenticationTests
{
    [TestMethod]
    public async Task LoginStoresUserAndTokensInSession()
    {
        var expected = Tokens("access-1", "refresh-1", "Cashier");
        var (client, session) = CreateClient(_ => JsonResponse(expected));

        var user = await client.LoginAsync("cashier", "strong-password");

        Assert.AreEqual("user", user.Username);
        Assert.AreEqual("access-1", session.AccessToken);
        Assert.AreEqual("refresh-1", session.RefreshToken);
        Assert.IsTrue(session.HasRole("cashier"));
        Assert.IsFalse(session.HasRole("Inventory"));
    }

    [TestMethod]
    public async Task UnauthorizedRequestRefreshesAndRetriesWithNewBearerToken()
    {
        var authorizedCalls = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login"))
                return JsonResponse(Tokens("access-1", "refresh-1", "Cashier"));
            if (request.RequestUri.AbsolutePath.EndsWith("/refresh"))
                return JsonResponse(Tokens("access-2", "refresh-2", "Cashier"));

            authorizedCalls++;
            var token = request.Headers.Authorization?.Parameter;
            return authorizedCalls == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(token == "access-2" ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        });
        var session = new AuthSession();
        var client = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, session);
        await client.LoginAsync("cashier", "strong-password");

        using var response = await client.SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Get, "api/test"));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("access-2", session.AccessToken);
        Assert.AreEqual("refresh-2", session.RefreshToken);
        Assert.AreEqual(2, authorizedCalls);
    }

    [TestMethod]
    public async Task LogoutClearsSessionWhenServerIsUnavailable()
    {
        var failLogout = false;
        var (client, session) = CreateClient(request =>
        {
            if (failLogout) throw new HttpRequestException("offline");
            return JsonResponse(Tokens("access-1", "refresh-1", "Admin"));
        });
        await client.LoginAsync("admin", "strong-password");
        failLogout = true;

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.LogoutAsync());

        Assert.IsFalse(session.IsAuthenticated);
        Assert.IsNull(session.AccessToken);
        Assert.IsNull(session.RefreshToken);
    }

    [TestMethod]
    public async Task RefreshResponseCannotRestoreSessionAfterLogout()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new AsyncStubHttpMessageHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login"))
                return JsonResponse(Tokens("access-1", "refresh-1", "Cashier"));
            if (request.RequestUri.AbsolutePath.EndsWith("/refresh"))
            {
                refreshStarted.SetResult();
                await releaseRefresh.Task;
                return JsonResponse(Tokens("access-2", "refresh-2", "Cashier"));
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        var session = new AuthSession();
        var client = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, session);
        await client.LoginAsync("cashier", "strong-password");

        var request = client.SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Get, "api/test"));
        await refreshStarted.Task;
        session.Clear();
        releaseRefresh.SetResult();

        await Assert.ThrowsExactlyAsync<ApiClientException>(() => request);
        Assert.IsFalse(session.IsAuthenticated);
        Assert.IsNull(session.AccessToken);
    }

    [TestMethod]
    [DataRow("Cashier", "Dashboard,Sales")]
    [DataRow("Inventory", "Dashboard,InventoryProducts,InventoryProducts,InventoryAddProduct,InventoryReceiveStock,InventorySuppliers,InventoryMovements,Reports")]
    public async Task DashboardNavigationMatchesRole(string role, string expectedTags)
    {
        var (client, session) = CreateClient(_ => JsonResponse(Tokens("access", "refresh", role)));
        await client.LoginAsync("user", "strong-password");
        var store = new StoreState(
            new StorePersistenceService(Path.Combine(Path.GetTempPath(), $"bpnv-auth-{Guid.NewGuid():N}.json")),
            seedPrototypeData: false);

        var viewModel = new DashboardViewModel(store, client, new StoreApiClient(client), session);

        CollectionAssert.AreEqual(
            expectedTags.Split(','),
            viewModel.NavItems.Select(item => item.Tag).ToArray());

        foreach (var item in viewModel.NavItems)
        {
            viewModel.SelectNavItem(item);
            Assert.IsNotNull(viewModel.CurrentPage, $"Navigation did not create a page for {item.Tag}.");
        }
    }

    private static (AuthApiClient Client, AuthSession Session) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var session = new AuthSession();
        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory))
        {
            BaseAddress = new Uri("https://test/")
        };
        return (new AuthApiClient(httpClient, session), session);
    }

    private static TokenResponse Tokens(string accessToken, string refreshToken, params string[] roles) => new(
        accessToken,
        DateTime.UtcNow.AddMinutes(15),
        refreshToken,
        DateTime.UtcNow.AddDays(7),
        new AuthenticatedUser(Guid.NewGuid(), "user", "Test User", roles, false));

    private static HttpResponseMessage JsonResponse(TokenResponse response) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(response)
    };
}

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(responseFactory(request));
}

internal sealed class AsyncStubHttpMessageHandler(
    Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => responseFactory(request);
}
