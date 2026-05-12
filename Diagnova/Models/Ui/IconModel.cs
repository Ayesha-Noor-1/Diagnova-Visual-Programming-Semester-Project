namespace Diagnova.Models.Ui;

/// <summary>SVG icon request for the <c>_Icon</c> partial view.</summary>
public sealed class IconModel
{
    public required string Id { get; init; }

    public int Size { get; init; } = 24;

    public string CssClass { get; init; } = "dn-icon";

    public string? AccessibilityLabel { get; init; }
}
