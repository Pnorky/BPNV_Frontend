using System.Net;
using System.Net.Http.Json;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class SuppliersViewModelTests
{
    [TestMethod]
    public async Task LoadsFiltersAndCreatesDatabaseSuppliers()
    {
        var imported = new SupplierResponse(Guid.NewGuid(), "Imported Vendor", "Maria", "0917", true);
        var created = new SupplierResponse(Guid.NewGuid(), "New Vendor", "Jose", null, true);
        CreateSupplierRequest? posted = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(new TokenResponse(
                "access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
                new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory User", ["Inventory"], false)));
            if (request.Method == HttpMethod.Get) return Json(new[] { imported });
            posted = request.Content!.ReadFromJsonAsync<CreateSupplierRequest>().GetAwaiter().GetResult();
            return Json(created);
        });
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, new AuthSession());
        await auth.LoginAsync("inventory", "password");
        var notifications = new TestNotificationService();
        var viewModel = new SuppliersViewModel(new StoreApiClient(auth), notifications);
        await WaitUntilIdle(viewModel);

        Assert.AreEqual("Imported Vendor", viewModel.FilteredSuppliers.Single().Name);
        viewModel.SearchText = "Maria";
        Assert.AreEqual("Imported Vendor", viewModel.FilteredSuppliers.Single().Name);

        viewModel.SearchText = "";
        await viewModel.CreateSupplierAsync();
        Assert.AreEqual("Error", notifications.Notifications.Single().Type);
        notifications.Notifications.Clear();

        viewModel.SupplierName = " New Vendor ";
        viewModel.ContactPerson = " Jose ";
        await viewModel.CreateSupplierAsync();

        Assert.AreEqual("New Vendor", posted!.Name);
        Assert.AreEqual("Jose", posted.ContactPerson);
        CollectionAssert.AreEqual(new[] { "Imported Vendor", "New Vendor" }, viewModel.FilteredSuppliers.Select(item => item.Name).ToArray());
        Assert.AreEqual("", viewModel.SupplierName);
        Assert.AreEqual("Success", notifications.Notifications.Single().Type);
        Assert.AreEqual("Supplier created", notifications.Notifications.Single().Title);
    }

    private static async Task WaitUntilIdle(SuppliersViewModel viewModel)
    {
        while (viewModel.IsBusy) await Task.Delay(5);
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
