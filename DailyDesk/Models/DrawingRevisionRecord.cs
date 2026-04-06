namespace DailyDesk.Models;

/// <summary>
/// Represents a single revision of an electrical drawing, tracking the revision
/// number, authorship, current signoff state, and package reference for transmittal.
/// Implements the revision-tracking requirements specified in AGENT_REPLY_GUIDE.md
/// (Electrical Drafting Workflows — Revision Control and Audit Trail).
/// </summary>
public sealed class DrawingRevisionRecord
{
    /// <summary>Unique identifier for this revision record.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Identifier of the drawing this revision belongs to.</summary>
    public string DrawingId { get; set; } = string.Empty;

    /// <summary>
    /// Revision designator (e.g. "A", "B", "1", "2") as used on the drawing title block.
    /// </summary>
    public string RevisionNumber { get; set; } = string.Empty;

    /// <summary>Description of the changes made in this revision.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Name or identifier of the person who issued this revision.</summary>
    public string IssuedBy { get; set; } = string.Empty;

    /// <summary>Current signoff state of this revision in the approval workflow.</summary>
    public DrawingSignoffState State { get; set; } = DrawingSignoffState.Draft;

    /// <summary>Timestamp when this revision was created or formally issued.</summary>
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Reference to the transmittal package this revision is part of (for package handoff tracking).
    /// Empty when the revision has not yet been added to a package.
    /// </summary>
    public string PackageRef { get; set; } = string.Empty;
}
