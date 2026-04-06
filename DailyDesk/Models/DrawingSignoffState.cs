namespace DailyDesk.Models;

/// <summary>
/// Represents the signoff state of an electrical drawing revision in the
/// approval routing workflow described in AGENT_REPLY_GUIDE.md (Electrical
/// Drafting Workflows — Revision Control and Audit Trail).
/// </summary>
public enum DrawingSignoffState
{
    /// <summary>Drawing is in initial authoring; not yet submitted for review.</summary>
    Draft,

    /// <summary>Drawing has been submitted and is under active review.</summary>
    InReview,

    /// <summary>Drawing has passed all review gates and is approved for issue.</summary>
    Approved,

    /// <summary>Drawing has been rejected and requires rework before resubmission.</summary>
    Rejected,

    /// <summary>Drawing has been superseded by a later revision and is no longer current.</summary>
    Superseded,
}
