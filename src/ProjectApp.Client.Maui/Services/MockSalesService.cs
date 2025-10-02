namespace ProjectApp.Client.Maui.Services;

public class MockSalesService : ISalesService
{
    public async Task<SalesResult> SubmitSaleAsync(SaleDraft draft, CancellationToken ct = default)
    {
        // Simulate processing
        await Task.Delay(300, ct);
        return SalesResult.Ok(); // mock success
    }
}
