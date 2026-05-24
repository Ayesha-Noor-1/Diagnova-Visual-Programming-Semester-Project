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

    Task<bool> SendOneTapSosAsync(
        string toEmail,
        string toName,
        string userName,
        string condensedProfile,
        double? latitude,
        double? longitude,
        string? locationMapsUrl,
        IReadOnlyList<Models.EmergencyFacilityClientDto> facilities,
        string? note = null);
}