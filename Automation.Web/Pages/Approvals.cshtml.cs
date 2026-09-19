using Automation.Sbc;
using Automation.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Automation.Web.Pages;

public class ApprovalsModel : PageModel
{
    private readonly DraftService drafts_;

    public ApprovalsModel(DraftService drafts)
    {
        drafts_ = drafts;
    }

    public IReadOnlyList<ApprovalRecord> Approvals { get; private set; } = [];

    public void OnGet()
    {
        Approvals = drafts_.Approvals();
    }
}
