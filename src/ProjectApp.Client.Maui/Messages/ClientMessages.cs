namespace ProjectApp.Client.Maui.Messages;

public record ClientPickedMessage(int ClientId, string Name);
public record ClientUpdatedMessage(int ClientId, string Name, string? Phone, string? Inn, ProjectApp.Client.Maui.Models.ClientType Type);
public record ClientCreatedMessage(int ClientId, string Name);
