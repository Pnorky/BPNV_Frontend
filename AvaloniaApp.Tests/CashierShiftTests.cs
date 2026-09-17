using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class CashierShiftTests
{
    [TestMethod]
    public async Task ClockStatusFailureInvalidatesPreviouslyOpenSession()
    {
        var failStatus = false;
        var open = Session(ApiCashierShiftSessionStatus.Open);
        var (api, _) = await CreateApiAsync(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/clock-status"))
            {
                if (failStatus) throw new HttpRequestException("offline");
                return Json(Status(open));
            }
            throw new InvalidOperationException(request.RequestUri.AbsolutePath);
        });
        var state = new CashierShiftState(api);

        Assert.IsTrue(await state.RefreshAsync());
        Assert.IsTrue(state.CanCheckout);

        failStatus = true;
        Assert.IsFalse(await state.RefreshAsync());

        Assert.IsNull(state.ClockStatus);
        Assert.IsFalse(state.CanCheckout);
        StringAssert.Contains(state.ErrorMessage, "unknown");
    }

    [TestMethod]
    public async Task ClockOutRetryReusesIdempotencyKeyAndKeepsSessionOnFailure()
    {
        var open = Session(ApiCashierShiftSessionStatus.Open);
        var closed = Session(ApiCashierShiftSessionStatus.ClosedPendingRemittance) with
        {
            ClockedOutAtUtc = DateTime.UtcNow,
            CloseType = ApiCashierShiftCloseType.CashierClockOut,
            ExpectedRemittance = 250m
        };
        var attempts = 0;
        var bodies = new List<string>();
        var (api, _) = await CreateApiAsync(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/clock-status")) return Json(Status(open));
            if (request.RequestUri.AbsolutePath.EndsWith("/clock-out"))
            {
                bodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                if (attempts++ == 0) throw new HttpRequestException("response lost");
                return Json(closed);
            }
            throw new InvalidOperationException(request.RequestUri.AbsolutePath);
        });
        var state = new CashierShiftState(api);
        await state.RefreshAsync();

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => state.ClockOutAsync());
        Assert.IsTrue(state.IsClockedIn);

        await state.ClockOutAsync();

        Assert.IsFalse(state.IsClockedIn);
        Assert.AreEqual(2, bodies.Count);
        Assert.AreEqual(ReadIdempotencyKey(bodies[0]), ReadIdempotencyKey(bodies[1]));
    }

    [TestMethod]
    public async Task ClockInRetryReusesOriginalKeyAndOpeningFloat()
    {
        var open = Session(ApiCashierShiftSessionStatus.Open);
        var attempts = 0;
        var bodies = new List<string>();
        var (api, _) = await CreateApiAsync(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/clock-in"))
            {
                bodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                if (attempts++ == 0) throw new HttpRequestException("response lost");
                return Json(open);
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/clock-status")) return Json(Status(open));
            throw new InvalidOperationException(request.RequestUri.AbsolutePath);
        });
        var state = new CashierShiftState(api);

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => state.ClockInAsync(1000m));
        Assert.AreEqual(1000m, state.PendingClockInOpeningFloat);
        await state.ClockInAsync(2000m);

        Assert.AreEqual(2, bodies.Count);
        Assert.AreEqual(ReadIdempotencyKey(bodies[0]), ReadIdempotencyKey(bodies[1]));
        Assert.AreEqual(1000m, ReadDecimal(bodies[0], "openingCashFloat"));
        Assert.AreEqual(1000m, ReadDecimal(bodies[1], "openingCashFloat"));
        Assert.IsNull(state.PendingClockInOpeningFloat);
    }

    [TestMethod]
    public async Task SalesCheckoutTracksSharedOpenSessionState()
    {
        var open = Session(ApiCashierShiftSessionStatus.Open);
        var (api, _) = await CreateApiAsync(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/cashier-shifts/clock-status" => Json(Status(open)),
            "/api/products/pos" => Json(new PagedResponse<PosProductResponse>([], 1, 200, 0)),
            "/api/employees" => Json<IReadOnlyList<EmployeeResponse>>([]),
            _ => throw new InvalidOperationException(request.RequestUri.AbsolutePath)
        });
        var state = new CashierShiftState(api);
        using var sales = new SalesViewModel(api, new TestNotificationService(), state);

        Assert.IsFalse(sales.CanCheckout);
        await state.RefreshAsync();

        Assert.IsTrue(sales.CanCheckout);
        Assert.IsFalse(sales.ShowShiftGate);
    }

    [TestMethod]
    public async Task NotificationRefreshPreservesInboxAndWarnsOncePerFailureEpisode()
    {
        var fail = false;
        var notification = new AdminNotificationResponse(
            Guid.NewGuid(), "ClockOut", "Shift closed", "Review remittance", Guid.NewGuid(), DateTime.UtcNow, false);
        var (api, _) = await CreateApiAsync(request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/admin-notifications"))
                throw new InvalidOperationException(request.RequestUri.AbsolutePath);
            if (fail) throw new HttpRequestException("offline");
            return Json(new AdminNotificationPageResponse([notification], 1, 20, 1, 1));
        });
        var toasts = new TestNotificationService();
        using var state = new AdminNotificationState(api, toasts);

        Assert.IsTrue(await state.RefreshAsync());
        fail = true;
        Assert.IsFalse(await state.RefreshAsync());
        Assert.IsFalse(await state.RefreshAsync());

        Assert.AreEqual(1, state.Items.Count);
        Assert.AreEqual(notification.Id, state.Items[0].Id);
        Assert.AreEqual(1, toasts.Notifications.Count(item => item.Type == "Warning"));
    }

    [TestMethod]
    public void ReplacementAllowsAdministrativeRecoveryButNotOpenOrNormallyClosedSession()
    {
        var definition = new ShiftDefinitionResponse(
            Guid.NewGuid(), "Shift 1", new TimeOnly(6, 0), new TimeOnly(14, 0), true);
        var date = DateOnly.FromDateTime(StoreDateTime.StoreToday);
        var open = new ResolvedDailyShiftRow(date, definition, null, null, null, Session(ApiCashierShiftSessionStatus.Open));
        var normalClose = new ResolvedDailyShiftRow(date, definition, null, null, null,
            Session(ApiCashierShiftSessionStatus.ClosedPendingRemittance) with { CloseType = ApiCashierShiftCloseType.CashierClockOut });
        var administrativeClose = new ResolvedDailyShiftRow(date, definition, null, null, null,
            Session(ApiCashierShiftSessionStatus.ClosedPendingRemittance) with { CloseType = ApiCashierShiftCloseType.AdministrativeClockOut });
        var unused = new ResolvedDailyShiftRow(date, definition, null, null, null, null);

        Assert.IsFalse(open.CanReplace);
        Assert.IsFalse(normalClose.CanReplace);
        Assert.IsTrue(administrativeClose.CanReplace);
        Assert.IsTrue(unused.CanReplace);
    }

    private static async Task<(StoreApiClient Api, AuthSession Session)> CreateApiAsync(
        Func<HttpRequestMessage, HttpResponseMessage> authenticatedResponse)
    {
        var session = new AuthSession();
        var auth = new AuthApiClient(new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(new TokenResponse(
                "access", DateTime.UtcNow.AddMinutes(10), "refresh", DateTime.UtcNow.AddDays(1),
                new AuthenticatedUser(Guid.NewGuid(), "cashier", "Test Cashier", ["Cashier", "Admin"], false)));
            return authenticatedResponse(request);
        })) { BaseAddress = new Uri("https://test/") }, session);
        await auth.LoginAsync("cashier", "password");
        return (new StoreApiClient(auth), session);
    }

    private static CashierClockStatusResponse Status(CashierShiftSessionResponse openSession) => new(
        DateTime.UtcNow, DateTimeOffset.Now, null, openSession, null, null, false, null);

    private static CashierShiftSessionResponse Session(ApiCashierShiftSessionStatus status)
    {
        var now = DateTime.UtcNow;
        return new CashierShiftSessionResponse(
            Id: Guid.NewGuid(), ScheduleId: Guid.NewGuid(), AssignmentOverrideId: null,
            ShiftDefinitionId: Guid.NewGuid(), CashierUserId: Guid.NewGuid(), CashierName: "Test Cashier",
            BusinessDate: DateOnly.FromDateTime(now), ShiftName: "Shift 1",
            ScheduledStartAtUtc: now.AddHours(-2), ScheduledEndAtUtc: now.AddHours(6),
            ClockedInAtUtc: now.AddHours(-1), ClockedOutAtUtc: null, ClockedOutByUserId: null,
            Status: status, CloseType: null, OpeningCashFloat: 1000m, ExpectedTerminalCash: null,
            WorkedMinutes: null, ClockInVarianceMinutes: 60, ClockOutVarianceMinutes: null,
            TotalSales: null, CashSales: null, GCashSales: null, CashRefunds: null, CashPayouts: null,
            TransactionCount: null, ExpectedRemittance: null, ActualRemittance: null, Variance: null,
            CashFloatReturned: null, RemittanceNote: null, RemittanceRecordedAtUtc: null,
            RemittanceRecordedByUserId: null, CashFloatConfirmedAtUtc: null, CashFloatConfirmedByUserId: null);
    }

    private static Guid ReadIdempotencyKey(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("idempotencyKey").GetGuid();

    private static decimal ReadDecimal(string json, string propertyName) =>
        JsonDocument.Parse(json).RootElement.GetProperty(propertyName).GetDecimal();

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
