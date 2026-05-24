namespace Diagnova.Services;

public static class ConditionTipFallbacks
{
    private static readonly Dictionary<string, (string Diet, string Exercise, string Lifestyle)> ByCondition =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["diabetes"] = (
                "Choose low-GI foods today: lentils, oats, or non-starchy vegetables with lean protein to help steady blood sugar.",
                "Take a 15–20 minute walk after your largest meal to support glucose control.",
                "Check your feet daily for cuts or redness, and keep glucose monitoring supplies within reach."
            ),
            ["hypertension"] = (
                "Limit added salt today; flavor meals with herbs, lemon, and garlic instead.",
                "Try gentle cardio for 20 minutes—brisk walking is enough to support heart health.",
                "Practice 5 minutes of slow breathing when stressed; it can help lower blood pressure temporarily."
            ),
            ["asthma"] = (
                "Stay hydrated and avoid known trigger foods if reflux worsens your asthma.",
                "Warm up slowly before activity; cool down afterward to reduce airway irritation.",
                "Keep your rescue inhaler accessible and note air quality before outdoor exercise."
            ),
            ["heart"] = (
                "Focus on heart-healthy fats: olive oil, nuts, and fish; limit fried and processed foods today.",
                "Aim for moderate activity your clinician approves—consistency matters more than intensity.",
                "If you have chest discomfort, shortness of breath, or dizziness, seek urgent care immediately."
            ),
        };

    public static (string Title, string Body) Get(string category, IReadOnlyList<string> conditions)
    {
        var key = MatchConditionKey(conditions);
        if (key is not null && ByCondition.TryGetValue(key, out var tips))
        {
            var body = category switch
            {
                "exercise" => tips.Exercise,
                "lifestyle" => tips.Lifestyle,
                _ => tips.Diet,
            };
            return ($"Daily {CategoryLabel(category)} tip", body);
        }

        return category switch
        {
            "exercise" => (
                "Move a little today",
                "A 15-minute walk or light stretching session supports circulation and mood—stop if you feel pain or dizziness."
            ),
            "lifestyle" => (
                "Small habit, big impact",
                "Drink an extra glass of water and aim for 7–8 hours of sleep tonight to support recovery and focus."
            ),
            _ => (
                "Eat the rainbow",
                "Add one extra serving of vegetables or fruit today for fiber, vitamins, and steady energy."
            ),
        };
    }

    private static string? MatchConditionKey(IReadOnlyList<string> conditions)
    {
        foreach (var c in conditions)
        {
            var lower = c.ToLowerInvariant();
            if (lower.Contains("diabet")) return "diabetes";
            if (lower.Contains("hypertens") || lower.Contains("blood pressure")) return "hypertension";
            if (lower.Contains("asthma")) return "asthma";
            if (lower.Contains("heart") || lower.Contains("cardiac")) return "heart";
        }
        return null;
    }

    public static string CategoryLabel(string category) => category switch
    {
        "exercise" => "Exercise",
        "lifestyle" => "Lifestyle",
        _ => "Diet",
    };

    public static string CategoryForToday() =>
        (DateTime.UtcNow.DayOfYear % 3) switch
        {
            1 => "exercise",
            2 => "lifestyle",
            _ => "diet",
        };
}
