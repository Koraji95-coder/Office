namespace DailyDesk.Models;

/// <summary>
/// Records a single event in the audit trail for an electrical drawing revision.
/// Captures who performed an action, which drawing and revision were affected,
/// and the state transition that occurred.
/// Implements the audit trail requirements specified in AGENT_REPLY_GUIDE.md
/// (Electrical Drafting Workflows — Revision Control and Audit Trail).
/// </summary>
public sealed class AuditTrailEntry
{
    /// <summary>Unique identifier for this audit entry.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Identifier of the drawing this audit entry relates to.</summary>
    public string DrawingId { get; set; } = string.Empty;

    /// <summary>Identifier of the revision this audit entry relates to.</summary>
    public string RevisionId { get; set; } = string.Empty;

    /// <summary>
    /// Description of the action that was taken (e.g. "submitted for review",
    /// "approved", "rejected", "issued to package").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Name or identifier of the person who performed the action.</summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>Signoff state the revision transitioned from.</summary>
    public DrawingSignoffState FromState { get; set; }

    /// <summary>Signoff state the revision transitioned to.</summary>
    public DrawingSignoffState ToState { get; set; }

    /// <summary>Optional notes or comments recorded with this audit event.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Timestamp when the action occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
