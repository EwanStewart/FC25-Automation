using Automation.Sbc;
using Automation.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Automation.Web.Pages;

public class ChallengeModel : PageModel
{
    private readonly DraftService drafts_;

    public ChallengeModel(DraftService drafts)
    {
        drafts_ = drafts;
    }

    public DraftedChallenge? Drafted { get; private set; }

    public long Budget { get; private set; } = SolverSite.DEFAULT_BUDGET;

    public IActionResult OnGet(int id, long? budget)
    {
        Budget = budget ?? SolverSite.DEFAULT_BUDGET;
        Drafted = drafts_.Draft(id, Budget);

        return Drafted is null ? NotFound() : Page();
    }

    public IActionResult OnPost(int id, long? budget)
    {
        var drafted = drafts_.Draft(id, budget ?? SolverSite.DEFAULT_BUDGET);
        IActionResult result;

        if (drafted is null || drafted.Squad.Outcome != SolveOutcome.Solved)
        {
            result = BadRequest("that challenge has no drafted squad to approve");
        }
        else
        {
            drafts_.Approve(drafted);
            result = RedirectToPage("/Approvals");
        }

        return result;
    }

    public int Chemistry(int slot)
    {
        return Drafted?.Squad.Assessment?.Chemistry.SlotPoints[slot] ?? 0;
    }
}
