using System.Net.Http.Json;

namespace ProjectApp.Client.Maui.Services;

public class ApiCostingService : ICostingService
{
    private readonly HttpClient _httpClient;

    public ApiCostingService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ProjectAppApi");
    }

    public async Task<List<CostingSessionDto>> GetSessionsAsync(int supplyId)
    {
        var response = await _httpClient.GetAsync($"/api/costing/sessions?supplyId={supplyId}");
        response.EnsureSuccessStatusCode();
        
        var sessions = await response.Content.ReadFromJsonAsync<List<CostingSessionDto>>();
        return sessions ?? new List<CostingSessionDto>();
    }

    public async Task<CostingSessionDetailsDto> GetSessionDetailsAsync(int sessionId)
    {
        var response = await _httpClient.GetAsync($"/api/costing/sessions/{sessionId}");
        response.EnsureSuccessStatusCode();
        
        var details = await response.Content.ReadFromJsonAsync<CostingSessionDetailsDto>();
        return details ?? throw new Exception("Failed to load session details");
    }

    public async Task<CostingSessionDto> CreateSessionAsync(CreateCostingSessionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/costing/sessions", request);
        response.EnsureSuccessStatusCode();
        
        var session = await response.Content.ReadFromJsonAsync<CostingSessionDto>();
        return session ?? throw new Exception("Failed to create session");
    }

    public async Task<RecalculateResultDto> RecalculateAsync(int sessionId)
    {
        var response = await _httpClient.PostAsync($"/api/costing/sessions/{sessionId}/recalculate", null);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<RecalculateResultDto>();
        return result ?? throw new Exception("Failed to recalculate");
    }

    public async Task<FinalizeResultDto> FinalizeAsync(int sessionId)
    {
        var response = await _httpClient.PostAsync($"/api/costing/sessions/{sessionId}/finalize", null);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<FinalizeResultDto>();
        return result ?? throw new Exception("Failed to finalize");
    }
}
