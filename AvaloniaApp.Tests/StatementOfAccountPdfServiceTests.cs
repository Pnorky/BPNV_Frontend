using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class StatementOfAccountPdfServiceTests
{
    [TestMethod]
    public void ExportCreatesBrandedPdfForChargesAndPayments()
    {
        var customerId = Guid.NewGuid();
        var customer = new CustomerResponse(customerId, "ACCT-001", "Sample Customer", "Bayombong", null,
            null, null, true, 1, 1250, null);
        var statement = new CustomerStatementResponse(customer, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1),
            DateTime.UtcNow, 1000, 500, 250, 1250,
            [new(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "DIESEL", "001937", "TJN 800", 10, 50, 0, 500)],
            [new(Guid.NewGuid(), "CPAY-000001", customerId, 250, ApiCustomerPaymentMethod.Cash, null, null,
                Guid.NewGuid(), "Cashier", Guid.NewGuid(), DateTime.UtcNow)]);
        using var output = new MemoryStream();

        StatementOfAccountPdfService.Export(statement, output);

        Assert.IsTrue(output.Length > 1000);
        CollectionAssert.AreEqual("%PDF"u8.ToArray(), output.ToArray()[..4]);
    }

    [TestMethod]
    public void SuggestedFileNameUsesCustomerAndEndingMonth()
    {
        var customer = new CustomerResponse(Guid.NewGuid(), "ACCT-001", "ACME/Fleet", null, null,
            null, null, true, 1, 0, null);
        var statement = new CustomerStatementResponse(customer, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31),
            DateTime.UtcNow, 0, 0, 0, 0, [], []);

        Assert.AreEqual("BPNV_ACME-Fleet_March2026.pdf", StatementOfAccountViewModel.BuildSuggestedFileName(statement));
    }
}
