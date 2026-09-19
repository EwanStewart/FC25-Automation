using Automation.Sbc;
using Automation.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Automation.Web.Pages;

public class ClubModel : PageModel
{
    private readonly DraftService drafts_;

    public ClubModel(DraftService drafts)
    {
        drafts_ = drafts;
    }

    public IReadOnlyList<SquadPlayer> Players { get; private set; } = [];

    public IReadOnlyList<long> Excluded { get; private set; } = [];

    [TempData] public string? Message { get; set; }

    public void OnGet()
    {
        Players = drafts_.ClubPlayers();
        Excluded = drafts_.Exclusions();
    }

    public IActionResult OnPostToggle(long playerId, bool excluded)
    {
        drafts_.SetExcluded(playerId, excluded);
        Message = excluded
            ? "Player excluded. The solver will redraft without them."
            : "Player included again. The solver will redraft with them.";

        return RedirectToPage();
    }
}
