using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace Diagnova.Models;

public class MedicalProfile
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRequired]
    public string UserId { get; set; } = string.Empty; // Links to Identity User

    // Personal Identity
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public int Age => CalculateAge(DateOfBirth);
    public string Gender { get; set; } = string.Empty;
    public string? ProfilePhotoUrl { get; set; }

    // Physical Health Metrics
    public double WeightKg { get; set; }
    public double HeightCm { get; set; }
    public string BloodType { get; set; } = string.Empty;
    public double BMI => CalculateBmi(WeightKg, HeightCm);

    // Medical Background
    public List<string> PreExistingConditions { get; set; } = new();
    public string CurrentMedications { get; set; } = string.Empty;
    public List<string> Allergies { get; set; } = new();
    public string SmokingHabit { get; set; } = "Never";
    public string AlcoholHabit { get; set; } = "Never";

    // Health Goals
    public string PrimaryGoal { get; set; } = string.Empty;
    public string ActivityLevel { get; set; } = "Sedentary";
    public int? DailyCalorieTarget { get; set; }

    // Emergency & Contact Info
    // Emergency & Contact Info
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactEmail { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;
    public string CountryRegion { get; set; } = string.Empty;

    // Security
    public string RecoveryMethod { get; set; } = string.Empty;
    public string? SecurityQuestion { get; set; }
    public string? SecurityAnswer { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }

    private static double CalculateBmi(double weightKg, double heightCm)
    {
        if (heightCm <= 0) return 0;
        var heightM = heightCm / 100;
        return Math.Round(weightKg / (heightM * heightM), 1);
    }
}