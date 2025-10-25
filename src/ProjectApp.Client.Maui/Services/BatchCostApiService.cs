using System.Net.Http.Json;
using System.Text.Json;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Services;

public class BatchCostApiService : IBatchCostService
{
    private readonly HttpClient _http;
    private readonly ILogger<BatchCostApiService>? _logger;

    public BatchCostApiService(HttpClient http, ILogger<BatchCostApiService>? logger = null)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<BatchCostItemDto>> GetItemsAsync(int supplyId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"/api/batch-cost/items/{supplyId}", ct);
            response.EnsureSuccessStatusCode();
            
            var items = await response.Content.ReadFromJsonAsync<List<BatchCostItemDto>>(ct);
            return items ?? new List<BatchCostItemDto>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get batch cost items for supply {SupplyId}", supplyId);
            throw;
        }
    }

    public async Task AddItemAsync(int supplyId, string productName, int quantity, decimal priceRub, CancellationToken ct = default)
    {
        try
        {
            var request = new
            {
                SupplyId = supplyId,
                BatchId = (int?)null,
                ProductName = productName,
                Quantity = quantity,
                PriceRub = priceRub
            };

            var response = await _http.PostAsJsonAsync("/api/batch-cost/items", request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add item to supply {SupplyId}", supplyId);
            throw;
        }
    }

    public async Task DeleteItemAsync(int itemId, int supplyId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.DeleteAsync($"/api/batch-cost/items/{itemId}?supplyId={supplyId}", ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete item {ItemId}", itemId);
            throw;
        }
    }

    public async Task RecalculateAsync(int supplyId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsync($"/api/batch-cost/recalculate/{supplyId}", null, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to recalculate supply {SupplyId}", supplyId);
            throw;
        }
    }

    public async Task<decimal> GetTotalCostAsync(int supplyId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"/api/batch-cost/total/{supplyId}", ct);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<TotalCostResponse>(ct);
            return result?.TotalCost ?? 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get total cost for supply {SupplyId}", supplyId);
            throw;
        }
    }

    private class TotalCostResponse
    {
        public decimal TotalCost { get; set; }
    }
}
