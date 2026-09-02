using System.Net;
using System.Net.Http.Json;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views;

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
        var createdOnServer = false;
        var loadCalls = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(new TokenResponse(
                "access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
                new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory User", ["Inventory"], false)));
            if (request.Method == HttpMethod.Get)
            {
                loadCalls++;
                return Json(createdOnServer ? new[] { imported, created } : [imported]);
            }
            posted = request.Content!.ReadFromJsonAsync<CreateSupplierRequest>().GetAwaiter().GetResult();
            createdOnServer = true;
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
        Assert.AreEqual(2, loadCalls);
        Assert.AreEqual("", viewModel.SupplierName);
        Assert.AreEqual("Success", notifications.Notifications.Single().Type);
        Assert.AreEqual("Supplier created", notifications.Notifications.Single().Title);
    }

    [TestMethod]
    public async Task UpdateDeactivateAndReactivateReloadAuthoritativeSupplierState()
    {
        var supplier = new SupplierResponse(Guid.NewGuid(), "Supplier", "Ana", "0917", true);
        var serverSupplier = supplier;
        var loadCalls = 0;
        UpdateSupplierRequest? updateRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(new TokenResponse(
                "access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
                new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory User", ["Inventory"], false)));
            if (request.Method == HttpMethod.Get)
            {
                loadCalls++;
                return Json(new[] { serverSupplier });
            }
            if (request.Method == HttpMethod.Put)
            {
                updateRequest = request.Content!.ReadFromJsonAsync<UpdateSupplierRequest>().GetAwaiter().GetResult();
                serverSupplier = serverSupplier with
                {
                    Name = updateRequest!.Name,
                    ContactPerson = updateRequest.ContactPerson,
                    Phone = updateRequest.Phone
                };
                return Json(serverSupplier);
            }
            if (request.Method == HttpMethod.Delete)
            {
                serverSupplier = serverSupplier with { IsActive = false };
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/reactivate"))
            {
                serverSupplier = serverSupplier with { IsActive = true };
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            throw new InvalidOperationException(request.RequestUri.ToString());
        });
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, new AuthSession());
        await auth.LoginAsync("inventory", "password");
        var notifications = new TestNotificationService();
        var viewModel = new SuppliersViewModel(new StoreApiClient(auth), notifications);
        await WaitUntilIdle(viewModel);

        await viewModel.UpdateSupplierAsync(supplier, " Updated ", " Contact ", " 0999 ");
        var updated = viewModel.FilteredSuppliers.Single();
        await viewModel.DeactivateSupplierAsync(updated);
        var inactive = viewModel.FilteredSuppliers.Single();
        await viewModel.ReactivateSupplierAsync(inactive);

        Assert.AreEqual("Updated", updateRequest!.Name);
        Assert.AreEqual("Contact", updateRequest.ContactPerson);
        Assert.AreEqual("0999", updateRequest.Phone);
        Assert.IsTrue(viewModel.FilteredSuppliers.Single().IsActive);
        Assert.AreEqual(4, loadCalls);
        CollectionAssert.AreEqual(
            new[] { "Supplier updated", "Supplier deactivated", "Supplier reactivated" },
            notifications.Notifications.Select(notification => notification.Title).ToArray());
    }

    [TestMethod]
    public async Task DeactivationRequiresConfirmation()
    {
        var supplier = new SupplierResponse(Guid.NewGuid(), "Supplier", null, null, true);
        var deleteCalls = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(new TokenResponse(
                "access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
                new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory User", ["Inventory"], false)));
            if (request.Method == HttpMethod.Delete)
            {
                deleteCalls++;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return Json(new[] { supplier with { IsActive = deleteCalls == 0 } });
        });
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, new AuthSession());
        await auth.LoginAsync("inventory", "password");
        var viewModel = new SuppliersViewModel(new StoreApiClient(auth), new TestNotificationService());
        await WaitUntilIdle(viewModel);

        await SuppliersView.ConfirmDeactivationAsync(supplier, viewModel, () => Task.FromResult(false));
        Assert.AreEqual(0, deleteCalls);

        await SuppliersView.ConfirmDeactivationAsync(supplier, viewModel, () => Task.FromResult(true));
        Assert.AreEqual(1, deleteCalls);
        Assert.IsFalse(viewModel.FilteredSuppliers.Single().IsActive);
    }

    private static async Task WaitUntilIdle(SuppliersViewModel viewModel)
    {
        while (viewModel.IsBusy) await Task.Delay(5);
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
