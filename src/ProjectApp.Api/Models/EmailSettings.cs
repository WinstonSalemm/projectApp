namespace ProjectApp.Api.Models;

/// <summary>
/// Настройки Email-уведомлений
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// Email отправителя
    /// </summary>
    public string From { get; set; } = string.Empty;
    
    /// <summary>
    /// Имя отправителя
    /// </summary>
    public string FromName { get; set; } = "ProjectApp";
    
    /// <summary>
    /// Email получателя (владелец)
    /// </summary>
    public string To { get; set; } = string.Empty;
    
    /// <summary>
    /// SMTP сервер
    /// </summary>
    public string SmtpHost { get; set; } = "smtp.yandex.ru";
    
    /// <summary>
    /// SMTP порт
    /// </summary>
    public int SmtpPort { get; set; } = 587;
    
    /// <summary>
    /// Логин SMTP
    /// </summary>
    public string SmtpUsername { get; set; } = string.Empty;
    
    /// <summary>
    /// Пароль SMTP
    /// </summary>
    public string SmtpPassword { get; set; } = string.Empty;
    
    /// <summary>
    /// Использовать SSL
    /// </summary>
    public bool UseSsl { get; set; } = true;
    
    /// <summary>
    /// Включены ли Email-уведомления
    /// </summary>
    public bool Enabled { get; set; } = true;
}
