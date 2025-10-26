using System.Net.Http.Json;

namespace ProjectApp.Client.Maui.Services;

public class ApiSuppliesService : ISuppliesService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiSuppliesService(
        IHttpClientFactory httpClientFactory,
        AppSettings settings,
        AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }
    
    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) 
            ? "http://localhost:5028" 
            : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        return client;
    }

    public async Task<List<SupplyDto>> GetSuppliesAsync(string registerType)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"/api/supplies?registerType={registerType}");
        response.EnsureSuccessStatusCode();
        
        var supplies = await response.Content.ReadFromJsonAsync<List<SupplyDto>>();
        return supplies ?? new List<SupplyDto>();
    }

    public async Task<SupplyDto> CreateSupplyAsync(string code)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"API: Creating supply with code {code}");
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/supplies", new { Code = code });
            
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
        var client = CreateClient();
        var response = await client.DeleteAsync($"/api/supplies/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task TransferToIm40Async(int id)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"/api/supplies/{id}/transfer-to-im40", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<SupplyItemDto>> GetSupplyItemsAsync(int supplyId)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"/api/supplies/{supplyId}/items");
        response.EnsureSuccessStatusCode();
        
        var items = await response.Content.ReadFromJsonAsync<List<SupplyItemDto>>();
        return items ?? new List<SupplyItemDto>();
    }

    public async Task AddSupplyItemAsync(int supplyId, string name, int quantity, decimal priceRub, string? category = null)
    {
        var client = CreateClient();
        var dto = new { Name = name, Quantity = quantity, PriceRub = priceRub, Category = category };
        var response = await client.PostAsJsonAsync($"/api/supplies/{supplyId}/items", dto);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSupplyItemAsync(int supplyId, int itemId)
    {
        var client = CreateClient();
        var response = await client.DeleteAsync($"/api/supplies/{supplyId}/items/{itemId}");
        response.EnsureSuccessStatusCode();
    }
}
