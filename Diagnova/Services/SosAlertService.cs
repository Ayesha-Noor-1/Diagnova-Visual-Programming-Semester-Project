using Diagnova.Models;
using MongoDB.Driver;

namespace Diagnova.Services;

public sealed class SosAlertService
{
    private readonly MongoDbService _mongoDb;
    private readonly IEmailService _email;
    private readonly EmergencyPlacesService _places;

    public SosAlertService(MongoDbService mongoDb, IEmailService email, EmergencyPlacesService places)
    {
        _mongoDb = mongoDb;
        _email = email;
        _places = places;
    }

    public async Task<SosTriggerResponse> TriggerAsync(
        string userId,
        double? latitude,
        double? longitude,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var profile = await _mongoDb.MedicalProfiles
            .Find(p => p.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return new SosTriggerResponse
            {
                Ok = false,
                Error = "Medical profile not found. Complete registration first.",
            };
        }

        if (string.IsNullOrWhiteSpace(profile.EmergencyContactEmail))
        {
            return new SosTriggerResponse
            {
                Ok = false,
                Error = "No emergency contact email configured. Add one in your health profile.",
            };
        }

        var condensed = MedicalProfileSummary.BuildCondensed(profile);
        var facilities = Array.Empty<EmergencyFacilityClientDto>();

        if (latitude is not null && longitude is not null)
        {
            facilities = (await _places.FindNearbyHospitalsAsync(
                latitude.Value,
                longitude.Value,
                cancellationToken)).ToArray();
        }

        var locationMapsUrl = latitude is not null && longitude is not null
            ? $"https://www.google.com/maps?q={latitude.Value:F6},{longitude.Value:F6}"
            : null;

        var smsBody = BuildSmsBody(profile.FullName, latitude, longitude, condensed, facilities, note);
        var smsUrl = BuildSmsUrl(profile.EmergencyContactPhone, smsBody);

        var emailSent = await _email.SendOneTapSosAsync(
            profile.EmergencyContactEmail.Trim(),
            string.IsNullOrWhiteSpace(profile.EmergencyContactName) ? "Emergency Contact" : profile.EmergencyContactName.Trim(),
            string.IsNullOrWhiteSpace(profile.FullName) ? "Diagnova user" : profile.FullName.Trim(),
            condensed,
            latitude,
            longitude,
            locationMapsUrl,
            facilities,
            note);

        return new SosTriggerResponse
        {
            Ok = emailSent,
            EmailSent = emailSent,
            Error = emailSent ? null : "Could not send SOS email. Check SMTP settings.",
            Message = emailSent
                ? "SOS sent to your emergency contact with location and medical summary."
                : null,
            Latitude = latitude,
            Longitude = longitude,
            LocationMapsUrl = locationMapsUrl,
            SmsUrl = smsUrl,
            CondensedProfile = condensed,
            Facilities = facilities,
        };
    }

    private static string BuildSmsBody(
        string userName,
        double? lat,
        double? lng,
        string profile,
        IReadOnlyList<EmergencyFacilityClientDto> facilities,
        string? note)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("SOS from Diagnova - ");
        sb.Append(string.IsNullOrWhiteSpace(userName) ? "user" : userName.Trim());
        sb.Append(" needs help. ");
        if (lat is not null && lng is not null)
            sb.Append($"Location: https://maps.google.com/?q={lat:F6},{lng:F6}. ");
        if (!string.IsNullOrWhiteSpace(note))
            sb.Append(note.Trim()).Append(' ');
        if (facilities.Count > 0)
        {
            var nearest = facilities[0];
            sb.Append($"Nearest ER: {nearest.Name} ({nearest.DistanceKm} km). ");
        }
        sb.Append("Profile: ");
        var oneLine = profile.Replace('\n', ';').Replace('\r', ' ');
        if (oneLine.Length > 280)
            oneLine = oneLine[..280] + "…";
        sb.Append(oneLine);
        return sb.ToString();
    }

    private static string? BuildSmsUrl(string? phone, string body)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.Length < 7)
            return null;

        return $"sms:{digits}?body={Uri.EscapeDataString(body)}";
    }
}
