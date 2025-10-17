namespace ProjectApp.Api.Integrations.Email;

/// <summary>
/// Интерфейс для отправки Email-уведомлений
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Отправить простое текстовое письмо
    /// </summary>
    Task<bool> SendEmailAsync(string subject, string body, CancellationToken ct = default);
    
    /// <summary>
    /// Отправить HTML письмо
    /// </summary>
    Task<bool> SendHtmlEmailAsync(string subject, string htmlBody, CancellationToken ct = default);
    
    /// <summary>
    /// Отправить письмо владельцу
    /// </summary>
    Task<bool> SendToOwnerAsync(string subject, string htmlBody, CancellationToken ct = default);
}
