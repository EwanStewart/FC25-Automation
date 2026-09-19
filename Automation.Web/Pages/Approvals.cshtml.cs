using Automation.Sbc;
using Automation.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Automation.Web.Pages;

public class ApprovalsModel : PageModel
{
    private const string CONFIRM_PHRASE = "LIVE";

    private readonly DraftService drafts_;
    private readonly FulfilmentLauncher launcher_;

    public ApprovalsModel(DraftService drafts, FulfilmentLauncher launcher)
    {
        drafts_ = drafts;
        launcher_ = launcher;
    }

    public IReadOnlyList<ApprovalRecord> Approvals { get; private set; } = [];

    [TempData] public string? Message { get; set; }

    public void OnGet()
    {
        Approvals = drafts_.Approvals();
    }

    public IActionResult OnPostRemove(int approvalId)
    {
        drafts_.RemoveApproval(approvalId);
        Message = $"Approval {approvalId} removed.";

        return RedirectToPage();
    }

    public IActionResult OnPost(bool live, string? confirm)
    {
        var refused = live && !string.Equals(confirm, CONFIRM_PHRASE, StringComparison.Ordinal);

        Message = refused
            ? "Live fulfilment refused: tick the confirmation box before running live."
            : launcher_.Start(live);

        return RedirectToPage();
    }
}
