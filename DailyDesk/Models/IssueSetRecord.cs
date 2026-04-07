namespace DailyDesk.Models;

/// <summary>
/// Represents a set of drawing revisions submitted together for approval as a single
/// issue package. Implements the issue-set handling requirements specified in
/// AGENT_REPLY_GUIDE.md (Approval Routing and Workflow Fit — Issue-Set Handling).
/// </summary>
public sealed class IssueSetRecord
{
    /// <summary>Unique identifier for this issue set.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Reference identifier for the drawing set or project this issue belongs to.
    /// </summary>
    public string DrawingSetRef { get; set; } = string.Empty;

    /// <summary>Name or identifier of the person who created and submitted this issue set.</summary>
    public string IssuedBy { get; set; } = string.Empty;

    /// <summary>Current approval state of this issue set.</summary>
    public IssueSetState State { get; set; } = IssueSetState.Pending;

    /// <summary>Timestamp when this issue set was created.</summary>
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Identifiers of the drawing revision records included in this issue set.
    /// </summary>
    public List<string> RevisionIds { get; set; } = new();

    /// <summary>
    /// Reason provided when the issue set was rejected. Empty when not rejected.
    /// </summary>
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the transmittal package this issue set is associated with.
    /// Empty until the issue set is approved and added to a package.
    /// </summary>
    public string PackageRef { get; set; } = string.Empty;
}
