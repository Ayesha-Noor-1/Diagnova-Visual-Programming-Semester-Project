namespace Diagnova.Services;

public interface IOpenFdaService
{
    Task<OpenFdaDrugLabel?> SearchDrugLabelAsync(string query, CancellationToken cancellationToken = default);
}

public sealed class OpenFdaDrugLabel
{
    public string? BrandName { get; init; }

    public string? GenericName { get; init; }

    public string? Purpose { get; init; }

    public string? DosageAndAdministration { get; init; }

    public string? Warnings { get; init; }

    public string? AdverseReactions { get; init; }

    public string? Contraindications { get; init; }
}
