using System.Net;
using System.Net.Http.Json;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class AddProductViewModelTests
{
    [TestMethod]
    public async Task NewProductSupplierSelectorExcludesInactiveSuppliers()
    {
        var active = new SupplierResponse(Guid.NewGuid(), "Active Supplier", null, null, true);
        var inactive = new SupplierResponse(Guid.NewGuid(), "Inactive Supplier", null, null, false);
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(new TokenResponse(
                "access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
                new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory User", ["Inventory"], false)));
            return Json(new[] { inactive, active });
        });
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, new AuthSession());
        await auth.LoginAsync("inventory", "password");
        var viewModel = new AddProductViewModel(new StoreApiClient(auth), new TestNotificationService());
        while (viewModel.IsBusy) await Task.Delay(5);

        Assert.AreEqual(active, viewModel.Suppliers.Single());
        Assert.AreEqual(active, viewModel.SelectedSupplier);
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
