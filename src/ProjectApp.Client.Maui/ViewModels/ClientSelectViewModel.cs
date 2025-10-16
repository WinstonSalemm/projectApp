using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ClientSelectViewModel : ObservableObject
{
    private readonly ILogger<ClientSelectViewModel> _logger;
    private readonly AppSettings _settings;

    [ObservableProperty] private string? query;
    [ObservableProperty] private string? selectedType;
    [ObservableProperty] private bool isBusy;

    public class ClientRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Inn { get; set; }
        public string Type { get; set; } = string.Empty;
        public string TypeLabel => Type == "Legal" ? "Юр.лицо" : "Физ.лицо";
        public bool HasInn => !string.IsNullOrWhiteSpace(Inn);
    }

    public ObservableCollection<ClientRow> Clients { get; } = new();

    public ClientSelectViewModel(AppSettings settings, ILogger<ClientSelectViewModel> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    [RelayCommand]
    public async Task Search()
    {
        try
        {
            IsBusy = true;
            Clients.Clear();

            var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl;
            var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(Query)) queryParams.Add($"q={Uri.EscapeDataString(Query)}");
            if (!string.IsNullOrWhiteSpace(SelectedType)) queryParams.Add($"type={SelectedType}");
            queryParams.Add("size=100");

            var qs = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var url = $"/api/clients{qs}";

            _logger.LogInformation("[ClientSelectViewModel] Search: url={Url}", url);
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[ClientSelectViewModel] Search failed: {StatusCode}", response.StatusCode);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<ClientsResponse>();
            if (result?.Items != null)
            {
                foreach (var item in result.Items)
                {
                    Clients.Add(new ClientRow
                    {
                        Id = item.Id,
                        Name = item.Name ?? "",
                        Phone = item.Phone,
                        Inn = item.Inn,
                        Type = item.Type ?? "Individual"
                    });
                }
            }

            _logger.LogInformation("[ClientSelectViewModel] Search completed: {Count} clients", Clients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClientSelectViewModel] Search failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AddClient()
    {
        // TODO: Open ClientCreatePage
        await Task.CompletedTask;
    }

    private class ClientsResponse
    {
        public List<ClientItem>? Items { get; set; }
        public int Total { get; set; }
    }

    private class ClientItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Inn { get; set; }
        public string? Type { get; set; }
    }
}
