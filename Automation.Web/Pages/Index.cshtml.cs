using Automation.Sbc;
using Automation.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Automation.Web.Pages;

public class IndexModel : PageModel
{
    private readonly DraftService drafts_;

    public IndexModel(DraftService drafts)
    {
        drafts_ = drafts;
    }

    public IReadOnlyList<DraftedChallenge> Drafted { get; private set; } = [];

    public long Budget { get; private set; } = SolverSite.DEFAULT_BUDGET;

    public DateTime? SolvedAt { get; private set; }

    public void OnGet(long? budget)
    {
        Budget = budget ?? SolverSite.DEFAULT_BUDGET;
        Drafted = drafts_.DraftAll(Budget);
        SolvedAt = drafts_.SolvedAt();
    }

    public IActionResult OnPostRegenerate(long? budget)
    {
        drafts_.Regenerate(budget ?? SolverSite.DEFAULT_BUDGET);

        return RedirectToPage();
    }
}
