using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Integrations.Email;

/// <summary>
/// Сервис для отправки Email-уведомлений через SMTP
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Отправить простое текстовое письмо
    /// </summary>
    public async Task<bool> SendEmailAsync(string subject, string body, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Email отправка отключена в настройках");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.From));
            message.To.Add(MailboxAddress.Parse(_settings.To));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            return await SendMessageAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки Email: {Subject}", subject);
            return false;
        }
    }

    /// <summary>
    /// Отправить HTML письмо
    /// </summary>
    public async Task<bool> SendHtmlEmailAsync(string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Email отправка отключена в настройках");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.From));
            message.To.Add(MailboxAddress.Parse(_settings.To));
            message.Subject = subject;
            
            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            message.Body = builder.ToMessageBody();

            return await SendMessageAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки HTML Email: {Subject}", subject);
            return false;
        }
    }

    /// <summary>
    /// Отправить письмо владельцу (alias для SendHtmlEmailAsync)
    /// </summary>
    public async Task<bool> SendToOwnerAsync(string subject, string htmlBody, CancellationToken ct = default)
    {
        return await SendHtmlEmailAsync(subject, htmlBody, ct);
    }

    /// <summary>
    /// Внутренний метод отправки сообщения через SMTP
    /// </summary>
    private async Task<bool> SendMessageAsync(MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient();
        
        try
        {
            // Подключаемся к SMTP серверу
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, 
                _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);

            // Аутентификация
            await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, ct);

            // Отправка
            await client.SendAsync(message, ct);

            _logger.LogInformation("Email успешно отправлен: {Subject} → {To}", 
                message.Subject, _settings.To);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка SMTP отправки: {Host}:{Port}", 
                _settings.SmtpHost, _settings.SmtpPort);
            return false;
        }
        finally
        {
            await client.DisconnectAsync(true, ct);
        }
    }
}
