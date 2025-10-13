using System.IO;

namespace ProjectApp.Client.Maui.Services;

public class MockSalesService : ISalesService
{
    public async Task<SalesResult> SubmitSaleAsync(SaleDraft draft, CancellationToken ct = default)
    {
        // Simulate processing
        await Task.Delay(300, ct);
        return SalesResult.Ok(); // mock success
    }

    public async Task<bool> UploadSalePhotoAsync(int saleId, Stream photoStream, string fileName, CancellationToken ct = default)
    {
        // Mock: pretend upload success after short delay
        await Task.Delay(100, ct);
        return true;
    }
}

