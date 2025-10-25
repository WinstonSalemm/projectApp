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
        try
        {
            System.Diagnostics.Debug.WriteLine($"API: Creating supply with code {code}");
            var response = await _httpClient.PostAsJsonAsync("/api/supplies", new { Code = code });
            
            System.Diagnostics.Debug.WriteLine($"API Response Status: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API Error: {errorContent}");
                throw new Exception($"API returned {response.StatusCode}: {errorContent}");
            }
            
            var supply = await response.Content.ReadFromJsonAsync<SupplyDto>();
            
            if (supply == null)
            {
                System.Diagnostics.Debug.WriteLine("API returned null supply");
                throw new Exception("API returned null supply");
            }
            
            System.Diagnostics.Debug.WriteLine($"API: Supply created successfully - Id: {supply.Id}, Code: {supply.Code}, Type: {supply.RegisterType}");
            return supply;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateSupplyAsync exception: {ex}");
            throw;
        }
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
