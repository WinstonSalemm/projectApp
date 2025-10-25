using System.Net.Http.Json;

namespace ProjectApp.Client.Maui.Services;

public class ApiSuppliesService : ISuppliesService
{
    private readonly HttpClient _httpClient;

    public ApiSuppliesService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientNames.Api);
    }

    public async Task<List<SupplyDto>> GetSuppliesAsync(string registerType)
    {
        var response = await _httpClient.GetAsync($"/api/supplies?registerType={registerType}");
        response.EnsureSuccessStatusCode();
        
        var supplies = await response.Content.ReadFromJsonAsync<List<SupplyDto>>();
        return supplies ?? new List<SupplyDto>();
    }

    public async Task<SupplyDto> CreateSupplyAsync(string code)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/supplies", new { Code = code });
        response.EnsureSuccessStatusCode();
        
        var supply = await response.Content.ReadFromJsonAsync<SupplyDto>();
        return supply ?? throw new Exception("Failed to create supply");
    }

    public async Task DeleteSupplyAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"/api/supplies/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task TransferToIm40Async(int id)
    {
        var response = await _httpClient.PostAsync($"/api/supplies/{id}/transfer-to-im40", null);
        response.EnsureSuccessStatusCode();
    }
}
