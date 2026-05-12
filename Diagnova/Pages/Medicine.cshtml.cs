using Diagnova.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diagnova.Pages;

[Authorize]
public class MedicineModel : PageModel
{
    private readonly IOpenFdaService _fda;

    public MedicineModel(IOpenFdaService fda)
    {
        _fda = fda;
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
    }
}
