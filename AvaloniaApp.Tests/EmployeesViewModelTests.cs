using System.Net;
using System.Net.Http.Json;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class EmployeesViewModelTests
{
    [TestMethod]
    public async Task LoadsFiltersAndCreatesEmployeesWithServerGeneratedNumber()
    {
        var existing = new EmployeeResponse(Guid.NewGuid(), "EMP-000001", "Ana Cruz", true);
        var created = new EmployeeResponse(Guid.NewGuid(), "EMP-000002", "Ben Santos", true);
        CreateEmployeeRequest? posted = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(new TokenResponse(
                "access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
                new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory User", ["Inventory"], false)));
            if (request.Method == HttpMethod.Get) return Json(new[] { existing });
            posted = request.Content!.ReadFromJsonAsync<CreateEmployeeRequest>().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(created) };
        });
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, new AuthSession());
        await auth.LoginAsync("inventory", "password");
        var notifications = new TestNotificationService();
        var viewModel = new EmployeesViewModel(new StoreApiClient(auth), notifications);
        while (viewModel.IsBusy) await Task.Delay(5);

        viewModel.SearchText = "000001";
        Assert.AreEqual("Ana Cruz", viewModel.FilteredEmployees.Single().Name);
        viewModel.SearchText = "";
        viewModel.EmployeeName = " Ben Santos ";
        await viewModel.CreateEmployeeAsync();

        Assert.AreEqual("Ben Santos", posted!.Name);
        CollectionAssert.AreEqual(new[] { "Ana Cruz", "Ben Santos" }, viewModel.FilteredEmployees.Select(item => item.Name).ToArray());
        Assert.AreEqual("EMP-000002", viewModel.FilteredEmployees.Last().EmployeeNumber);
        Assert.AreEqual("Success", notifications.Notifications.Single().Type);
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
