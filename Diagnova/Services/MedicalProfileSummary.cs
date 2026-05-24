using Diagnova.Models;

namespace Diagnova.Services;

public static class MedicalProfileSummary
{
    public static string BuildCondensed(MedicalProfile profile)
    {
        var lines = new List<string>
        {
            $"Name: {NullIfEmpty(profile.FullName)}",
            $"Age: {(profile.DateOfBirth != default ? profile.Age.ToString() : "—")}",
            $"Gender: {NullIfEmpty(profile.Gender)}",
            $"Blood type: {NullIfEmpty(profile.BloodType)}",
            $"Weight: {(profile.WeightKg > 0 ? $"{profile.WeightKg} kg" : "—")}",
            $"Height: {(profile.HeightCm > 0 ? $"{profile.HeightCm} cm" : "—")}",
        };

        if (profile.PreExistingConditions.Count > 0)
            lines.Add($"Conditions: {string.Join(", ", profile.PreExistingConditions)}");
        if (profile.Allergies.Count > 0)
            lines.Add($"Allergies: {string.Join(", ", profile.Allergies)}");
        if (!string.IsNullOrWhiteSpace(profile.CurrentMedications))
            lines.Add($"Medications: {profile.CurrentMedications.Trim()}");

        lines.Add($"Smoking: {NullIfEmpty(profile.SmokingHabit)}");
        lines.Add($"Alcohol: {NullIfEmpty(profile.AlcoholHabit)}");

        if (!string.IsNullOrWhiteSpace(profile.CountryRegion))
            lines.Add($"Region: {profile.CountryRegion.Trim()}");

        if (profile.WeightKg > 0 && profile.HeightCm > 0)
            lines.Add($"BMI: {profile.BMI}");

        if (!string.IsNullOrWhiteSpace(profile.EmergencyContactName) ||
            !string.IsNullOrWhiteSpace(profile.EmergencyContactEmail))
        {
            lines.Add($"Emergency contact: {NullIfEmpty(profile.EmergencyContactName)} " +
                      $"({NullIfEmpty(profile.EmergencyContactEmail)})");
        }

        if (!string.IsNullOrWhiteSpace(profile.EmergencyContactPhone))
            lines.Add($"Emergency phone: {profile.EmergencyContactPhone.Trim()}");

        if (string.IsNullOrWhiteSpace(profile.CurrentMedications) &&
            profile.PreExistingConditions.Count == 0 &&
            profile.Allergies.Count == 0)
        {
            lines.Add("Conditions / allergies / medications: none recorded");
        }

        return string.Join("\n", lines);
    }

    private static string NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
}
