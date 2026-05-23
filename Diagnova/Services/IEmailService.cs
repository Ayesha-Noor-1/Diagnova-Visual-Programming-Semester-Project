namespace Diagnova.Services;

public interface IEmailService
{
    Task<bool> SendEmergencyAlertAsync(
        string toEmail,
        string toName,
        string userName,
        string userMessage,
        List<string> keywords,
        string? alarmingReason = null);
}