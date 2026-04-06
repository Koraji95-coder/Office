using System.Text.RegularExpressions;
using DailyDesk.Models;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify revision control and audit trail compliance
/// as specified in AGENT_REPLY_GUIDE.md chunk6 (Best Reply Patterns For
/// Electrical Drafting Workflows — Revision Control and Audit Trail).
///
/// The tests are structured in five groups:
///   1. Document compliance — verify AGENT_REPLY_GUIDE.md chunk6 contains all
///      required revision-control and audit-trail specification elements.
///   2. DrawingRevisionRecord model compliance — verify the model has all fields
///      required by the revision-tracking specification.
///   3. DrawingSignoffState enum compliance — verify all required signoff states
///      are present.
///   4. AuditTrailEntry model compliance — verify the model has all fields
///      required by the audit-trail specification.
///   5. Integration workflow tests — verify that the state-transition and audit
///      trail mechanics work correctly end-to-end.
/// </summary>
public sealed class RevisionControlAuditTrailTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "DailyDesk", "DailyDesk.csproj")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string GetAgentReplyGuidePath()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        return Path.Combine(root!, "DailyDesk", "AGENT_REPLY_GUIDE.md");
    }

    private static string ReadGuide()
    {
        var path = GetAgentReplyGuidePath();
        Assert.True(File.Exists(path), $"AGENT_REPLY_GUIDE.md not found at: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Extracts the body of the "Best Reply Patterns For Electrical Drafting Workflows"
    /// section (chunk6) from AGENT_REPLY_GUIDE.md.
    /// </summary>
    private static string ExtractElectricalDraftingSection(string guide)
    {
        const string sectionHeader = "## Best Reply Patterns For Electrical Drafting Workflows";
        var start = guide.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        // Find the next top-level "## " heading after the section start.
        var nextSection = guide.IndexOf("\n## ", start + sectionHeader.Length, StringComparison.Ordinal);
        return nextSection >= 0
            ? guide[start..nextSection]
            : guide[start..];
    }

    /// <summary>
    /// Extracts the body of the "Revision Control and Audit Trail" sub-pattern
    /// from the electrical drafting section.
    /// </summary>
    private static string ExtractRevisionControlPattern(string section)
    {
        const string patternHeader = "### Revision Control and Audit Trail";
        var start = section.IndexOf(patternHeader, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        // Find the next "### " sub-heading after this pattern.
        var nextPattern = section.IndexOf("\n### ", start + patternHeader.Length, StringComparison.Ordinal);
        return nextPattern >= 0
            ? section[start..nextPattern]
            : section[start..];
    }

    /// <summary>
    /// Helper that applies a revision state transition, records an audit entry,
    /// and returns the populated entry.
    /// </summary>
    private static AuditTrailEntry Transition(
        DrawingRevisionRecord revision,
        DrawingSignoffState toState,
        string actor,
        string action,
        string notes = "")
    {
        var entry = new AuditTrailEntry
        {
            DrawingId   = revision.DrawingId,
            RevisionId  = revision.Id,
            Action      = action,
            Actor       = actor,
            FromState   = revision.State,
            ToState     = toState,
            Notes       = notes,
            OccurredAt  = DateTimeOffset.UtcNow,
        };
        revision.State = toState;
        return entry;
    }

    // -----------------------------------------------------------------------
    // Group 1: AGENT_REPLY_GUIDE.md chunk6 document compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void AgentReplyGuide_Exists()
    {
        var path = GetAgentReplyGuidePath();
        Assert.True(File.Exists(path),
            $"AGENT_REPLY_GUIDE.md must exist at DailyDesk/AGENT_REPLY_GUIDE.md; not found at: {path}");
    }

    [Fact]
    public void AgentReplyGuide_ContainsElectricalDraftingWorkflowsSection()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        Assert.False(string.IsNullOrWhiteSpace(section),
            "AGENT_REPLY_GUIDE.md must contain a '## Best Reply Patterns For Electrical Drafting Workflows' section (chunk6)");
    }

    [Fact]
    public void AgentReplyGuide_ElectricalDraftingSection_ContainsRevisionControlAndAuditTrailPattern()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        Assert.Contains("### Revision Control and Audit Trail", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_RevisionControlPattern_ContainsRevisionTracking()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        var pattern = ExtractRevisionControlPattern(section);

        Assert.Contains("revision tracking", pattern, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_RevisionControlPattern_ContainsSignoffStates()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        var pattern = ExtractRevisionControlPattern(section);

        Assert.Contains("signoff states", pattern, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_RevisionControlPattern_ContainsAuditTrailRequirements()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        var pattern = ExtractRevisionControlPattern(section);

        Assert.Contains("audit trail requirements", pattern, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_RevisionControlPattern_ContainsPackageHandoffSteps()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        var pattern = ExtractRevisionControlPattern(section);

        Assert.Contains("package handoff steps", pattern, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_RevisionControlPattern_ExcludesCRMAndBillingScope()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        var pattern = ExtractRevisionControlPattern(section);

        // The spec must explicitly constrain scope away from CRM and billing.
        Assert.True(
            pattern.Contains("CRM", StringComparison.OrdinalIgnoreCase) ||
            pattern.Contains("billing", StringComparison.OrdinalIgnoreCase),
            "Revision Control pattern must reference 'CRM' and/or 'billing' as out-of-scope constraints");
    }

    [Fact]
    public void AgentReplyGuide_ElectricalDraftingSection_ContainsDrawingReviewRoutingPattern()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        Assert.Contains("### Drawing Review Routing", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalDraftingSection_ContainsIssueSetApprovalPattern()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        Assert.Contains("### Issue Set Approval", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalDraftingSection_ContainsProductionTransmittalPattern()
    {
        var guide   = ReadGuide();
        var section = ExtractElectricalDraftingSection(guide);
        Assert.Contains("### Production Transmittal Workflow", section, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Group 2: DrawingRevisionRecord model compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void DrawingRevisionRecord_HasId_ForUniqueTracking()
    {
        var record = new DrawingRevisionRecord();
        Assert.False(string.IsNullOrWhiteSpace(record.Id),
            "DrawingRevisionRecord must have a non-empty Id for unique revision tracking");
    }

    [Fact]
    public void DrawingRevisionRecord_HasDrawingId_ForRevisionTracking()
    {
        // DrawingId links the revision to its parent drawing — required for revision tracking.
        var record = new DrawingRevisionRecord { DrawingId = "DWG-001" };
        Assert.Equal("DWG-001", record.DrawingId);
    }

    [Fact]
    public void DrawingRevisionRecord_HasRevisionNumber_ForRevisionTracking()
    {
        // RevisionNumber carries the designator (e.g. "A", "1") that appears on the title block.
        var record = new DrawingRevisionRecord { RevisionNumber = "A" };
        Assert.Equal("A", record.RevisionNumber);
    }

    [Fact]
    public void DrawingRevisionRecord_HasIssuedBy_ForSignoffRequirements()
    {
        // IssuedBy captures who submitted the revision — required for signoff accountability.
        var record = new DrawingRevisionRecord { IssuedBy = "J.Smith" };
        Assert.Equal("J.Smith", record.IssuedBy);
    }

    [Fact]
    public void DrawingRevisionRecord_HasState_AsSignoffState()
    {
        // State carries the current signoff state in the approval workflow.
        var record = new DrawingRevisionRecord { State = DrawingSignoffState.InReview };
        Assert.Equal(DrawingSignoffState.InReview, record.State);
    }

    [Fact]
    public void DrawingRevisionRecord_DefaultState_IsDraft()
    {
        // New revisions must default to Draft — the starting state in the approval workflow.
        var record = new DrawingRevisionRecord();
        Assert.Equal(DrawingSignoffState.Draft, record.State);
    }

    [Fact]
    public void DrawingRevisionRecord_HasIssuedAt_ForAuditTimestamp()
    {
        // IssuedAt is the timestamp required for audit trail completeness.
        var now    = DateTimeOffset.UtcNow;
        var record = new DrawingRevisionRecord { IssuedAt = now };
        Assert.Equal(now, record.IssuedAt);
    }

    [Fact]
    public void DrawingRevisionRecord_HasPackageRef_ForHandoffTracking()
    {
        // PackageRef links the revision to a transmittal package — required for package handoff steps.
        var record = new DrawingRevisionRecord { PackageRef = "PKG-2026-001" };
        Assert.Equal("PKG-2026-001", record.PackageRef);
    }

    [Fact]
    public void DrawingRevisionRecord_DefaultPackageRef_IsEmpty()
    {
        // Before a revision is added to a package, PackageRef must be empty.
        var record = new DrawingRevisionRecord();
        Assert.Equal(string.Empty, record.PackageRef);
    }

    // -----------------------------------------------------------------------
    // Group 3: DrawingSignoffState enum compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void DrawingSignoffState_HasDraftState()
    {
        Assert.True(Enum.IsDefined(typeof(DrawingSignoffState), DrawingSignoffState.Draft),
            "DrawingSignoffState must include Draft — the initial authoring state");
    }

    [Fact]
    public void DrawingSignoffState_HasInReviewState()
    {
        Assert.True(Enum.IsDefined(typeof(DrawingSignoffState), DrawingSignoffState.InReview),
            "DrawingSignoffState must include InReview — the active review state");
    }

    [Fact]
    public void DrawingSignoffState_HasApprovedState()
    {
        Assert.True(Enum.IsDefined(typeof(DrawingSignoffState), DrawingSignoffState.Approved),
            "DrawingSignoffState must include Approved — the final acceptance state");
    }

    [Fact]
    public void DrawingSignoffState_HasRejectedState()
    {
        Assert.True(Enum.IsDefined(typeof(DrawingSignoffState), DrawingSignoffState.Rejected),
            "DrawingSignoffState must include Rejected — the rejection/rework state");
    }

    [Fact]
    public void DrawingSignoffState_HasSupersededState()
    {
        Assert.True(Enum.IsDefined(typeof(DrawingSignoffState), DrawingSignoffState.Superseded),
            "DrawingSignoffState must include Superseded — the state for revisions replaced by a later issue");
    }

    [Fact]
    public void DrawingSignoffState_HasExactlyFiveStates()
    {
        var states = Enum.GetValues<DrawingSignoffState>();
        Assert.Equal(5, states.Length);
    }

    // -----------------------------------------------------------------------
    // Group 4: AuditTrailEntry model compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void AuditTrailEntry_HasId_ForUniqueAuditRecord()
    {
        var entry = new AuditTrailEntry();
        Assert.False(string.IsNullOrWhiteSpace(entry.Id),
            "AuditTrailEntry must have a non-empty Id for unique audit record identification");
    }

    [Fact]
    public void AuditTrailEntry_HasDrawingId_LinkingToDrawing()
    {
        var entry = new AuditTrailEntry { DrawingId = "DWG-001" };
        Assert.Equal("DWG-001", entry.DrawingId);
    }

    [Fact]
    public void AuditTrailEntry_HasRevisionId_LinkingToRevision()
    {
        var entry = new AuditTrailEntry { RevisionId = "rev-abc" };
        Assert.Equal("rev-abc", entry.RevisionId);
    }

    [Fact]
    public void AuditTrailEntry_HasAction_ForAuditRecord()
    {
        // Action describes what happened — required for meaningful audit trail entries.
        var entry = new AuditTrailEntry { Action = "submitted for review" };
        Assert.Equal("submitted for review", entry.Action);
    }

    [Fact]
    public void AuditTrailEntry_HasActor_ForSignoffAccountability()
    {
        // Actor captures who performed the action — required for signoff audit trail.
        var entry = new AuditTrailEntry { Actor = "J.Smith" };
        Assert.Equal("J.Smith", entry.Actor);
    }

    [Fact]
    public void AuditTrailEntry_HasFromState_ForStateTransitionTracking()
    {
        var entry = new AuditTrailEntry { FromState = DrawingSignoffState.Draft };
        Assert.Equal(DrawingSignoffState.Draft, entry.FromState);
    }

    [Fact]
    public void AuditTrailEntry_HasToState_ForStateTransitionTracking()
    {
        var entry = new AuditTrailEntry { ToState = DrawingSignoffState.InReview };
        Assert.Equal(DrawingSignoffState.InReview, entry.ToState);
    }

    [Fact]
    public void AuditTrailEntry_HasOccurredAt_ForTimestampCompliance()
    {
        var now   = DateTimeOffset.UtcNow;
        var entry = new AuditTrailEntry { OccurredAt = now };
        Assert.Equal(now, entry.OccurredAt);
    }

    [Fact]
    public void AuditTrailEntry_HasNotes_ForOptionalComments()
    {
        var entry = new AuditTrailEntry { Notes = "Minor markup applied to single-line diagram." };
        Assert.Equal("Minor markup applied to single-line diagram.", entry.Notes);
    }

    // -----------------------------------------------------------------------
    // Group 5: Integration workflow tests
    // -----------------------------------------------------------------------

    [Fact]
    public void RevisionWorkflow_NewRevision_StartsInDraftState()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E001",
            RevisionNumber = "A",
            IssuedBy       = "J.Smith",
        };

        Assert.Equal(DrawingSignoffState.Draft, revision.State);
    }

    [Fact]
    public void RevisionWorkflow_DraftToInReview_GeneratesAuditEntry()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E001",
            RevisionNumber = "A",
            IssuedBy       = "J.Smith",
        };

        var entry = Transition(revision, DrawingSignoffState.InReview,
            actor: "J.Smith", action: "submitted for review");

        Assert.Equal(DrawingSignoffState.InReview, revision.State);
        Assert.Equal(DrawingSignoffState.Draft,    entry.FromState);
        Assert.Equal(DrawingSignoffState.InReview, entry.ToState);
        Assert.Equal("J.Smith",                    entry.Actor);
        Assert.Equal("submitted for review",       entry.Action);
        Assert.Equal("DWG-E001",                   entry.DrawingId);
    }

    [Fact]
    public void RevisionWorkflow_InReviewToApproved_GeneratesAuditEntry()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E001",
            RevisionNumber = "A",
            IssuedBy       = "J.Smith",
            State          = DrawingSignoffState.InReview,
        };

        var entry = Transition(revision, DrawingSignoffState.Approved,
            actor: "B.Jones", action: "approved");

        Assert.Equal(DrawingSignoffState.Approved, revision.State);
        Assert.Equal(DrawingSignoffState.InReview, entry.FromState);
        Assert.Equal(DrawingSignoffState.Approved, entry.ToState);
        Assert.Equal("B.Jones",                    entry.Actor);
    }

    [Fact]
    public void RevisionWorkflow_InReviewToRejected_GeneratesAuditEntry()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E001",
            RevisionNumber = "A",
            IssuedBy       = "J.Smith",
            State          = DrawingSignoffState.InReview,
        };

        var entry = Transition(revision, DrawingSignoffState.Rejected,
            actor: "B.Jones", action: "rejected",
            notes: "Conduit sizing incorrect on panel schedule.");

        Assert.Equal(DrawingSignoffState.Rejected, revision.State);
        Assert.Equal(DrawingSignoffState.InReview, entry.FromState);
        Assert.Equal(DrawingSignoffState.Rejected, entry.ToState);
        Assert.Equal("Conduit sizing incorrect on panel schedule.", entry.Notes);
    }

    [Fact]
    public void RevisionWorkflow_ApprovedRevisionSupersededByLaterRevision_GeneratesAuditEntry()
    {
        var revisionA = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E001",
            RevisionNumber = "A",
            IssuedBy       = "J.Smith",
            State          = DrawingSignoffState.Approved,
        };

        // Issuing revision B supersedes revision A.
        var entry = Transition(revisionA, DrawingSignoffState.Superseded,
            actor: "J.Smith", action: "superseded by revision B");

        Assert.Equal(DrawingSignoffState.Superseded, revisionA.State);
        Assert.Equal(DrawingSignoffState.Approved,   entry.FromState);
        Assert.Equal(DrawingSignoffState.Superseded, entry.ToState);
    }

    [Fact]
    public void RevisionWorkflow_AuditTrail_RecordsAllStateTransitions()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E002",
            RevisionNumber = "1",
            IssuedBy       = "A.Patel",
        };

        var auditTrail = new List<AuditTrailEntry>
        {
            Transition(revision, DrawingSignoffState.InReview,
                actor: "A.Patel", action: "submitted for review"),
            Transition(revision, DrawingSignoffState.Rejected,
                actor: "C.Wong",  action: "rejected",
                notes: "Missing earth fault loop impedance values."),
            Transition(revision, DrawingSignoffState.InReview,
                actor: "A.Patel", action: "resubmitted after rework"),
            Transition(revision, DrawingSignoffState.Approved,
                actor: "C.Wong",  action: "approved"),
        };

        Assert.Equal(4, auditTrail.Count);
        Assert.Equal(DrawingSignoffState.Approved, revision.State);

        // Verify the audit trail records a complete, ordered sequence of transitions.
        Assert.Equal(DrawingSignoffState.Draft,    auditTrail[0].FromState);
        Assert.Equal(DrawingSignoffState.InReview, auditTrail[0].ToState);

        Assert.Equal(DrawingSignoffState.InReview, auditTrail[1].FromState);
        Assert.Equal(DrawingSignoffState.Rejected, auditTrail[1].ToState);

        Assert.Equal(DrawingSignoffState.Rejected, auditTrail[2].FromState);
        Assert.Equal(DrawingSignoffState.InReview, auditTrail[2].ToState);

        Assert.Equal(DrawingSignoffState.InReview, auditTrail[3].FromState);
        Assert.Equal(DrawingSignoffState.Approved, auditTrail[3].ToState);
    }

    [Fact]
    public void RevisionWorkflow_AuditTrail_AllEntriesHaveDrawingIdAndRevisionId()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E003",
            RevisionNumber = "B",
            IssuedBy       = "M.Taylor",
        };

        var auditTrail = new List<AuditTrailEntry>
        {
            Transition(revision, DrawingSignoffState.InReview,
                actor: "M.Taylor", action: "submitted for review"),
            Transition(revision, DrawingSignoffState.Approved,
                actor: "L.Chen",   action: "approved"),
        };

        foreach (var entry in auditTrail)
        {
            Assert.Equal("DWG-E003",  entry.DrawingId);
            Assert.Equal(revision.Id, entry.RevisionId);
            Assert.False(string.IsNullOrWhiteSpace(entry.Actor));
            Assert.False(string.IsNullOrWhiteSpace(entry.Action));
        }
    }

    [Fact]
    public void RevisionWorkflow_PackageHandoff_AssignsPackageRefOnApproval()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E004",
            RevisionNumber = "C",
            IssuedBy       = "R.Nguyen",
            State          = DrawingSignoffState.Approved,
        };

        // Package handoff step: assign a transmittal package reference to the approved revision.
        revision.PackageRef = "PKG-2026-003";

        Assert.Equal("PKG-2026-003",               revision.PackageRef);
        Assert.Equal(DrawingSignoffState.Approved, revision.State);
    }
}
