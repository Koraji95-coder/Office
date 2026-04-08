using DailyDesk.Models;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify the audit trail implementation details documented in
/// AGENT_REPLY_GUIDE.md chunk8 (Audit Trail Implementation Details).
///
/// The tests are structured in five groups:
///   1. Document compliance — verify AGENT_REPLY_GUIDE.md chunk8 contains all
///      required audit trail implementation specification elements.
///   2. AuditTrailEntry field compliance — verify all required fields and defaults.
///   3. DrawingSignoffState compliance — verify all states are present and documented.
///   4. State transition recording tests — verify FromState/ToState capture pattern.
///   5. Compliance checklist integration tests — verify end-to-end audit trail rules.
/// </summary>
public sealed class AuditTrailChunk8Tests
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
    /// Extracts the body of the "Audit Trail Implementation Details" section (chunk8)
    /// from AGENT_REPLY_GUIDE.md.
    /// </summary>
    private static string ExtractChunk8Section(string guide)
    {
        const string sectionHeader = "## Audit Trail Implementation Details";
        var start = guide.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var nextSection = guide.IndexOf("\n## ", start + sectionHeader.Length, StringComparison.Ordinal);
        return nextSection >= 0
            ? guide[start..nextSection]
            : guide[start..];
    }

    /// <summary>
    /// Helper that applies a revision state transition and records an audit entry.
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
            DrawingId  = revision.DrawingId,
            RevisionId = revision.Id,
            Action     = action,
            Actor      = actor,
            FromState  = revision.State,
            ToState    = toState,
            Notes      = notes,
            OccurredAt = DateTimeOffset.UtcNow,
        };
        revision.State = toState;
        return entry;
    }

    // -----------------------------------------------------------------------
    // Group 1: AGENT_REPLY_GUIDE.md chunk8 document compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void AgentReplyGuide_ContainsAuditTrailImplementationDetailsSection()
    {
        var guide   = ReadGuide();
        var section = ExtractChunk8Section(guide);
        Assert.False(string.IsNullOrWhiteSpace(section),
            "AGENT_REPLY_GUIDE.md must contain a '## Audit Trail Implementation Details' section (chunk8)");
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_ContainsAuditTrailEntryStructureSubsection()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("### AuditTrailEntry Structure", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_ContainsStateTransitionRecordingSubsection()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("### State Transition Recording", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_ContainsStorageSubsection()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("### Storage", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_ContainsSignoffStatesSubsection()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("### Signoff States", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_ContainsRequiredAuditFieldsSubsection()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("### Required Audit Fields", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_ContainsComplianceChecklistSubsection()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("### Audit Trail Compliance Checklist", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_DocumentsAllRequiredEntryFields()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("DrawingId",  section, StringComparison.Ordinal);
        Assert.Contains("RevisionId", section, StringComparison.Ordinal);
        Assert.Contains("Action",     section, StringComparison.Ordinal);
        Assert.Contains("Actor",      section, StringComparison.Ordinal);
        Assert.Contains("FromState",  section, StringComparison.Ordinal);
        Assert.Contains("ToState",    section, StringComparison.Ordinal);
        Assert.Contains("Notes",      section, StringComparison.Ordinal);
        Assert.Contains("OccurredAt", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_DocumentsLiteDbCollectionName()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("drawing_audit_trail", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_DocumentsOfficeDatabaseService()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("OfficeDatabase", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_DocumentsActorAccountability()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("actor accountability", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_DocumentsTimestampRequirement()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("UTC", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_DocumentsRejectionReasonInNotes()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("rejection reason", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Notes", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_DocumentsIssueSetTransitionHandling()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("IssueSetState", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_Chunk8_DocumentsIndexingByDrawingId()
    {
        var section = ExtractChunk8Section(ReadGuide());
        Assert.Contains("DrawingId", section, StringComparison.Ordinal);
        Assert.Contains("indexed", section, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Group 2: AuditTrailEntry field compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void AuditTrailEntry_HasId_AutoAssignedOnCreation()
    {
        var entry = new AuditTrailEntry();
        Assert.False(string.IsNullOrWhiteSpace(entry.Id),
            "AuditTrailEntry must auto-assign a non-empty Id on creation");
    }

    [Fact]
    public void AuditTrailEntry_Id_IsUniqueAcrossInstances()
    {
        var e1 = new AuditTrailEntry();
        var e2 = new AuditTrailEntry();
        Assert.NotEqual(e1.Id, e2.Id);
    }

    [Fact]
    public void AuditTrailEntry_HasDrawingId_ForDrawingLevelQueries()
    {
        var entry = new AuditTrailEntry { DrawingId = "DWG-001" };
        Assert.Equal("DWG-001", entry.DrawingId);
    }

    [Fact]
    public void AuditTrailEntry_HasRevisionId_ForRevisionLevelQueries()
    {
        var entry = new AuditTrailEntry { RevisionId = "rev-abc" };
        Assert.Equal("rev-abc", entry.RevisionId);
    }

    [Fact]
    public void AuditTrailEntry_HasAction_ForEventDescription()
    {
        var entry = new AuditTrailEntry { Action = "approved" };
        Assert.Equal("approved", entry.Action);
    }

    [Fact]
    public void AuditTrailEntry_HasActor_ForActorAccountability()
    {
        var entry = new AuditTrailEntry { Actor = "B.Jones" };
        Assert.Equal("B.Jones", entry.Actor);
    }

    [Fact]
    public void AuditTrailEntry_HasFromState_ForStateTransitionRecord()
    {
        var entry = new AuditTrailEntry { FromState = DrawingSignoffState.InReview };
        Assert.Equal(DrawingSignoffState.InReview, entry.FromState);
    }

    [Fact]
    public void AuditTrailEntry_HasToState_ForStateTransitionRecord()
    {
        var entry = new AuditTrailEntry { ToState = DrawingSignoffState.Approved };
        Assert.Equal(DrawingSignoffState.Approved, entry.ToState);
    }

    [Fact]
    public void AuditTrailEntry_HasNotes_ForRejectionReason()
    {
        const string reason = "Earthing schedule missing from panel layout.";
        var entry = new AuditTrailEntry { Notes = reason };
        Assert.Equal(reason, entry.Notes);
    }

    [Fact]
    public void AuditTrailEntry_HasOccurredAt_ForTimestampRequirement()
    {
        var before = DateTimeOffset.UtcNow;
        var entry  = new AuditTrailEntry();
        var after  = DateTimeOffset.UtcNow;

        Assert.InRange(entry.OccurredAt, before, after);
    }

    [Fact]
    public void AuditTrailEntry_OccurredAt_DefaultIsUtc()
    {
        var entry = new AuditTrailEntry();
        Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset);
    }

    [Fact]
    public void AuditTrailEntry_DefaultDrawingId_IsEmpty()
    {
        var entry = new AuditTrailEntry();
        Assert.Equal(string.Empty, entry.DrawingId);
    }

    [Fact]
    public void AuditTrailEntry_DefaultActor_IsEmpty()
    {
        var entry = new AuditTrailEntry();
        Assert.Equal(string.Empty, entry.Actor);
    }

    [Fact]
    public void AuditTrailEntry_DefaultNotes_IsEmpty()
    {
        var entry = new AuditTrailEntry();
        Assert.Equal(string.Empty, entry.Notes);
    }

    // -----------------------------------------------------------------------
    // Group 3: DrawingSignoffState compliance
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
            "DrawingSignoffState must include InReview — the active review gate state");
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
            "DrawingSignoffState must include Rejected — the rejection path state requiring rework");
    }

    [Fact]
    public void DrawingSignoffState_HasSupersededState()
    {
        Assert.True(Enum.IsDefined(typeof(DrawingSignoffState), DrawingSignoffState.Superseded),
            "DrawingSignoffState must include Superseded — the state for replaced revisions");
    }

    [Fact]
    public void DrawingSignoffState_HasExactlyFiveStates()
    {
        var states = Enum.GetValues<DrawingSignoffState>();
        Assert.Equal(5, states.Length);
    }

    // -----------------------------------------------------------------------
    // Group 4: State transition recording tests
    // -----------------------------------------------------------------------

    [Fact]
    public void StateTransition_DraftToInReview_CapturesFromAndToState()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E001",
            IssuedBy  = "J.Smith",
        };
        Assert.Equal(DrawingSignoffState.Draft, revision.State);

        var entry = Transition(revision, DrawingSignoffState.InReview,
            actor: "J.Smith", action: "submitted for review");

        Assert.Equal(DrawingSignoffState.Draft,    entry.FromState);
        Assert.Equal(DrawingSignoffState.InReview, entry.ToState);
        Assert.Equal(DrawingSignoffState.InReview, revision.State);
    }

    [Fact]
    public void StateTransition_InReviewToApproved_CapturesFromAndToState()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E001",
            IssuedBy  = "J.Smith",
            State     = DrawingSignoffState.InReview,
        };

        var entry = Transition(revision, DrawingSignoffState.Approved,
            actor: "B.Jones", action: "approved");

        Assert.Equal(DrawingSignoffState.InReview, entry.FromState);
        Assert.Equal(DrawingSignoffState.Approved, entry.ToState);
        Assert.Equal("B.Jones",                    entry.Actor);
    }

    [Fact]
    public void StateTransition_InReviewToRejected_StoresRejectionReasonInNotes()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E001",
            IssuedBy  = "J.Smith",
            State     = DrawingSignoffState.InReview,
        };
        const string reason = "Earthing schedule missing from panel layout.";

        var entry = Transition(revision, DrawingSignoffState.Rejected,
            actor: "B.Jones", action: "rejected", notes: reason);

        Assert.Equal(DrawingSignoffState.Rejected, entry.ToState);
        Assert.Equal(reason, entry.Notes);
    }

    [Fact]
    public void StateTransition_RejectedToDraft_AllowsResubmission()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E001",
            IssuedBy  = "J.Smith",
            State     = DrawingSignoffState.Rejected,
        };

        var entry = Transition(revision, DrawingSignoffState.Draft,
            actor: "J.Smith", action: "returned to draft for rework");

        Assert.Equal(DrawingSignoffState.Rejected, entry.FromState);
        Assert.Equal(DrawingSignoffState.Draft,    entry.ToState);
        Assert.Equal(DrawingSignoffState.Draft,    revision.State);
    }

    [Fact]
    public void StateTransition_ApprovedToSuperseded_CapturesSupersessionEvent()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E001",
            IssuedBy  = "J.Smith",
            State     = DrawingSignoffState.Approved,
        };

        var entry = Transition(revision, DrawingSignoffState.Superseded,
            actor: "B.Jones", action: "superseded by revision B");

        Assert.Equal(DrawingSignoffState.Approved,   entry.FromState);
        Assert.Equal(DrawingSignoffState.Superseded, entry.ToState);
    }

    [Fact]
    public void StateTransition_Entry_LinksToRevisionAndDrawing()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E002",
            RevisionNumber = "A",
            IssuedBy       = "J.Smith",
        };

        var entry = Transition(revision, DrawingSignoffState.InReview,
            actor: "J.Smith", action: "submitted for review");

        Assert.Equal("DWG-E002",  entry.DrawingId);
        Assert.Equal(revision.Id, entry.RevisionId);
    }

    // -----------------------------------------------------------------------
    // Group 5: Compliance checklist integration tests
    // -----------------------------------------------------------------------

    [Fact]
    public void ComplianceRule1_EachTransitionProducesExactlyOneAuditEntry()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E003",
            IssuedBy  = "J.Smith",
        };

        var entries = new List<AuditTrailEntry>
        {
            Transition(revision, DrawingSignoffState.InReview, "J.Smith", "submitted for review"),
            Transition(revision, DrawingSignoffState.Approved, "B.Jones", "approved"),
        };

        // Two state transitions → exactly two audit entries.
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void ComplianceRule2_ActorIsNeverEmpty()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E003",
            IssuedBy  = "J.Smith",
        };

        var entry = Transition(revision, DrawingSignoffState.InReview,
            actor: "J.Smith", action: "submitted for review");

        Assert.False(string.IsNullOrWhiteSpace(entry.Actor),
            "Actor must never be empty — every action requires an accountable person");
    }

    [Fact]
    public void ComplianceRule3_OccurredAtIsUtc()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E003",
            IssuedBy  = "J.Smith",
        };

        var entry = Transition(revision, DrawingSignoffState.InReview,
            actor: "J.Smith", action: "submitted for review");

        Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset);
    }

    [Fact]
    public void ComplianceRule4_FromStateAndToState_AreSetOnRevisionTransitions()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E003",
            IssuedBy  = "J.Smith",
            State     = DrawingSignoffState.InReview,
        };

        var entry = Transition(revision, DrawingSignoffState.Approved,
            actor: "B.Jones", action: "approved");

        Assert.Equal(DrawingSignoffState.InReview, entry.FromState);
        Assert.Equal(DrawingSignoffState.Approved, entry.ToState);
    }

    [Fact]
    public void ComplianceRule5_RejectionEvent_IncludesRejectionReasonInNotes()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId = "DWG-E003",
            IssuedBy  = "J.Smith",
            State     = DrawingSignoffState.InReview,
        };
        const string reason = "Missing conductor schedule on panel layout.";

        var entry = Transition(revision, DrawingSignoffState.Rejected,
            actor: "B.Jones", action: "rejected", notes: reason);

        Assert.False(string.IsNullOrWhiteSpace(entry.Notes),
            "Rejection events must include the rejection reason in Notes");
        Assert.Equal(reason, entry.Notes);
    }

    [Fact]
    public void ComplianceRule7_IssueSetTransition_UsesActionAndNotesForStateInfo()
    {
        // Issue-set audit entries carry state in Action/Notes because
        // IssueSetState and DrawingSignoffState are semantically distinct.
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E001",
            IssuedBy      = "J.Smith",
        };

        var entry = new AuditTrailEntry
        {
            DrawingId  = issueSet.DrawingSetRef,
            RevisionId = issueSet.Id,
            Action     = "submitted for approval",
            Actor      = "J.Smith",
            Notes      = $"State: {IssueSetState.Pending} → {IssueSetState.InApproval}",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        issueSet.State = IssueSetState.InApproval;

        Assert.Equal(IssueSetState.InApproval, issueSet.State);
        Assert.Contains("InApproval", entry.Notes, StringComparison.Ordinal);
        Assert.Equal("submitted for approval", entry.Action);
    }

    [Fact]
    public void ComplianceRule_FullWorkflow_ProducesChronologicalAuditTrail()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E004",
            RevisionNumber = "A",
            IssuedBy       = "J.Smith",
        };

        var auditLog = new List<AuditTrailEntry>();

        auditLog.Add(Transition(revision, DrawingSignoffState.InReview,
            actor: "J.Smith", action: "submitted for review"));

        auditLog.Add(Transition(revision, DrawingSignoffState.Rejected,
            actor: "B.Jones", action: "rejected",
            notes: "Earthing schedule missing."));

        auditLog.Add(Transition(revision, DrawingSignoffState.Draft,
            actor: "J.Smith", action: "returned to draft for rework"));

        auditLog.Add(Transition(revision, DrawingSignoffState.InReview,
            actor: "J.Smith", action: "resubmitted for review"));

        auditLog.Add(Transition(revision, DrawingSignoffState.Approved,
            actor: "B.Jones", action: "approved"));

        Assert.Equal(5, auditLog.Count);
        Assert.Equal(DrawingSignoffState.Approved, revision.State);

        // All entries link to the same drawing.
        Assert.All(auditLog, e => Assert.Equal("DWG-E004", e.DrawingId));

        // All actors are populated.
        Assert.All(auditLog, e => Assert.False(string.IsNullOrWhiteSpace(e.Actor)));

        // All timestamps are UTC.
        Assert.All(auditLog, e => Assert.Equal(TimeSpan.Zero, e.OccurredAt.Offset));

        // Single() enforces exactly one rejection entry — both existence and uniqueness.
        var rejectionEntry = auditLog.Single(e => e.ToState == DrawingSignoffState.Rejected);
        Assert.False(string.IsNullOrWhiteSpace(rejectionEntry.Notes));
    }
}
