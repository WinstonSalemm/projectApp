namespace ProjectApp.Api.Integrations.Telegram;

public class TelegramSettings
{
    public string BotToken { get; set; } = string.Empty;
    public string SecretToken { get; set; } = "140606tl"; // used to validate webhook header
    public string? AllowedChatIds { get; set; } // comma-separated list; if null => allow all
    public string? PublicUrl { get; set; } // e.g., https://tranquil-upliftment-production.up.railway.app

    public HashSet<long> ParseAllowedChatIds()
    {
        var set = new HashSet<long>();
        if (string.IsNullOrWhiteSpace(AllowedChatIds)) return set;
        foreach (var part in AllowedChatIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, out var id)) set.Add(id);
        }
        return set;
    }
}
