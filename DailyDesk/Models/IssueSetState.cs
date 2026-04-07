namespace DailyDesk.Models;

/// <summary>
/// Represents the lifecycle state of an issue set in the approval workflow described
/// in AGENT_REPLY_GUIDE.md (Approval Routing and Workflow Fit — Issue-Set Handling).
/// </summary>
public enum IssueSetState
{
    /// <summary>Issue set has been created but not yet submitted for approval.</summary>
    Pending,

    /// <summary>Issue set has been submitted and is under active approval review.</summary>
    InApproval,

    /// <summary>Issue set has passed all approval gates and is approved for issue.</summary>
    Approved,

    /// <summary>Issue set has been rejected and requires rework before resubmission.</summary>
    Rejected,

    /// <summary>Issue set has been resubmitted after a previous rejection.</summary>
    Resubmitted,
}
