using System.Security.Claims;
using Diagnova.Models;
using Diagnova.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diagnova.Pages;

[Authorize]
public class MedicineModel : PageModel
{
    private readonly IOpenFdaService _fda;
    private readonly MongoDbService _mongoDb;

    public MedicineModel(IOpenFdaService fda, MongoDbService mongoDb)
    {
        _fda = fda;
        _mongoDb = mongoDb;
    }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    public OpenFdaDrugLabel? Result { get; private set; }

    public bool Searched { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Query))
            return;

        Searched = true;
        Result = await _fda.SearchDrugLabelAsync(Query, cancellationToken);

        if (Result is not null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                await _mongoDb.MedicineSearchHistory.InsertOneAsync(new MongoMedicineSearch
                {
                    UserId = userId,
                    Query = Query.Trim(),
                    SearchedAt = DateTimeOffset.UtcNow,
                    BrandName = Result.BrandName ?? Result.GenericName,
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
