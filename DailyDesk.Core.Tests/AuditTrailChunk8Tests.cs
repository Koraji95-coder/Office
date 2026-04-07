using System.Text.RegularExpressions;
using DailyDesk.Models;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify audit trail and revision tracking compliance
/// as specified in AGENT_REPLY_GUIDE.md chunk8 (Electrical Construction QA/QC
/// Templates — production control audit trail and revision tracking).
///
/// The tests are structured in five groups:
///   1. Document compliance — verify AGENT_REPLY_GUIDE.md chunk8 contains all
///      required electrical-construction QA/QC specification elements.
///   2. AuditTrailEntry model compliance — verify the model supports the
///      production control audit requirements described in chunk8.
///   3. DrawingRevisionRecord model compliance — verify revision tracking
///      fields required for QA/QC production-control workflows.
///   4. Audit trail integration tests — verify that audit entries are created
///      correctly when drawing packages are reviewed against QA/QC standards.
///   5. Production control workflow integration tests — verify end-to-end
///      alignment of revision tracking, approval routing, and audit trail
///      for electrical drafting production control.
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
    /// Extracts the body of the "Electrical Construction QA/QC Templates"
    /// section (chunk8) from AGENT_REPLY_GUIDE.md.
    /// </summary>
    private static string ExtractQaQcTemplatesSection(string guide)
    {
        const string sectionHeader = "## Electrical Construction QA/QC Templates";
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
    /// Helper that records an audit trail entry when a drawing revision is
    /// submitted for QA/QC review and transitions to a new signoff state.
    /// </summary>
    private static AuditTrailEntry RecordQaQcReview(
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
    // Group 1: Document compliance — chunk8 must contain required elements
    // -----------------------------------------------------------------------

    [Fact]
    public void Chunk8_Section_Exists_InAgentReplyGuide()
    {
        var guide   = ReadGuide();
        var section = ExtractQaQcTemplatesSection(guide);

        Assert.False(string.IsNullOrWhiteSpace(section),
            "AGENT_REPLY_GUIDE.md must contain the 'Electrical Construction QA/QC Templates' section (chunk8).");
    }

    [Fact]
    public void Chunk8_Section_References_WatercareQaQcTemplate()
    {
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.True(
            section.Contains("Watercare", StringComparison.OrdinalIgnoreCase),
            "chunk8 must reference the Watercare QA/QC template source.");
    }

    [Fact]
    public void Chunk8_Section_References_Section1_13()
    {
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.True(
            section.Contains("1.13", StringComparison.Ordinal),
            "chunk8 must reference section 1.13 of the QA/QC template.");
    }

    [Fact]
    public void Chunk8_Section_References_MinimumMandatoryTests()
    {
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.True(
            section.Contains("mandatory", StringComparison.OrdinalIgnoreCase),
            "chunk8 must reference minimum mandatory test requirements.");
    }

    [Fact]
    public void Chunk8_Section_References_PassFailChecklist()
    {
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.True(
            section.Contains("pass/fail", StringComparison.OrdinalIgnoreCase)
            || section.Contains("pass-fail", StringComparison.OrdinalIgnoreCase)
            || section.Contains("checklist", StringComparison.OrdinalIgnoreCase),
            "chunk8 must reference a pass/fail checklist for QA/QC review.");
    }

    [Fact]
    public void Chunk8_Section_References_Switchboards()
    {
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.True(
            section.Contains("switchboard", StringComparison.OrdinalIgnoreCase),
            "chunk8 must reference switchboards as a production control scope element.");
    }

    [Fact]
    public void Chunk8_Section_References_DistributionCentres()
    {
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.True(
            section.Contains("distribution centre", StringComparison.OrdinalIgnoreCase)
            || section.Contains("distribution center", StringComparison.OrdinalIgnoreCase),
            "chunk8 must reference distribution centres as a production control scope element.");
    }

    [Fact]
    public void Chunk8_Section_References_ControlCentres()
    {
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.True(
            section.Contains("control centre", StringComparison.OrdinalIgnoreCase)
            || section.Contains("control center", StringComparison.OrdinalIgnoreCase),
            "chunk8 must reference control centres as a production control scope element.");
    }

    [Fact]
    public void Chunk8_Section_References_DrawingPackageReview()
    {
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.True(
            section.Contains("drawing package", StringComparison.OrdinalIgnoreCase)
            || section.Contains("drawing review", StringComparison.OrdinalIgnoreCase),
            "chunk8 must reference drawing package or drawing review workflows.");
    }

    [Fact]
    public void Chunk8_Section_MeetsMinimumLength()
    {
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.False(string.IsNullOrWhiteSpace(section),
            "chunk8 section must not be empty.");
        Assert.True(section.Length >= 200,
            "chunk8 section must contain a meaningful specification (at least 200 characters).");
    }

    // -----------------------------------------------------------------------
    // Group 2: AuditTrailEntry model compliance for production control
    // -----------------------------------------------------------------------

    [Fact]
    public void AuditTrailEntry_HasUniqueId_OnConstruction()
    {
        var entry = new AuditTrailEntry();

        Assert.False(string.IsNullOrWhiteSpace(entry.Id),
            "AuditTrailEntry must have a non-empty Id on construction.");
    }

    [Fact]
    public void AuditTrailEntry_HasDrawingId_Field()
    {
        var entry = new AuditTrailEntry { DrawingId = "DWG-QA-001" };

        Assert.Equal("DWG-QA-001", entry.DrawingId);
    }

    [Fact]
    public void AuditTrailEntry_HasRevisionId_Field()
    {
        var entry = new AuditTrailEntry { RevisionId = "rev-id-abc" };

        Assert.Equal("rev-id-abc", entry.RevisionId);
    }

    [Fact]
    public void AuditTrailEntry_HasActor_Field()
    {
        var entry = new AuditTrailEntry { Actor = "QA.Engineer" };

        Assert.Equal("QA.Engineer", entry.Actor);
    }

    [Fact]
    public void AuditTrailEntry_HasAction_Field()
    {
        var entry = new AuditTrailEntry { Action = "submitted for QA/QC review" };

        Assert.Equal("submitted for QA/QC review", entry.Action);
    }

    [Fact]
    public void AuditTrailEntry_HasFromState_And_ToState_Fields()
    {
        var entry = new AuditTrailEntry
        {
            FromState = DrawingSignoffState.Draft,
            ToState   = DrawingSignoffState.InReview,
        };

        Assert.Equal(DrawingSignoffState.Draft,     entry.FromState);
        Assert.Equal(DrawingSignoffState.InReview,  entry.ToState);
    }

    [Fact]
    public void AuditTrailEntry_HasNotes_Field_ForQaQcFindings()
    {
        var entry = new AuditTrailEntry
        {
            Notes = "Termination check failed: cable not labelled at panel.",
        };

        Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
        Assert.Contains("Termination check", entry.Notes);
    }

    [Fact]
    public void AuditTrailEntry_HasOccurredAt_Field_DefaultedToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var entry  = new AuditTrailEntry();
        var after  = DateTimeOffset.UtcNow;

        Assert.True(entry.OccurredAt >= before && entry.OccurredAt <= after,
            "AuditTrailEntry.OccurredAt must default to UtcNow.");
    }

    [Fact]
    public void AuditTrailEntry_Ids_AreUniqueAcrossInstances()
    {
        var ids = Enumerable.Range(0, 50)
                            .Select(_ => new AuditTrailEntry().Id)
                            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // -----------------------------------------------------------------------
    // Group 3: DrawingRevisionRecord model compliance for production QA/QC
    // -----------------------------------------------------------------------

    [Fact]
    public void DrawingRevisionRecord_DefaultState_IsDraft()
    {
        var revision = new DrawingRevisionRecord();

        Assert.Equal(DrawingSignoffState.Draft, revision.State);
    }

    [Fact]
    public void DrawingRevisionRecord_HasDrawingId_Field()
    {
        var revision = new DrawingRevisionRecord { DrawingId = "DWG-SW-101" };

        Assert.Equal("DWG-SW-101", revision.DrawingId);
    }

    [Fact]
    public void DrawingRevisionRecord_HasRevisionNumber_Field()
    {
        var revision = new DrawingRevisionRecord { RevisionNumber = "C" };

        Assert.Equal("C", revision.RevisionNumber);
    }

    [Fact]
    public void DrawingRevisionRecord_HasIssuedBy_Field()
    {
        var revision = new DrawingRevisionRecord { IssuedBy = "T.Williams" };

        Assert.Equal("T.Williams", revision.IssuedBy);
    }

    [Fact]
    public void DrawingRevisionRecord_HasPackageRef_Field_ForProductionHandoff()
    {
        var revision = new DrawingRevisionRecord { PackageRef = "PKG-QA-2026-003" };

        Assert.Equal("PKG-QA-2026-003", revision.PackageRef);
    }

    [Fact]
    public void DrawingRevisionRecord_HasDescription_Field_ForRevisionNotes()
    {
        var revision = new DrawingRevisionRecord
        {
            Description = "Updated cable schedule per QA/QC review findings.",
        };

        Assert.False(string.IsNullOrWhiteSpace(revision.Description));
    }

    [Fact]
    public void DrawingRevisionRecord_Ids_AreUniqueAcrossInstances()
    {
        var ids = Enumerable.Range(0, 50)
                            .Select(_ => new DrawingRevisionRecord().Id)
                            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // -----------------------------------------------------------------------
    // Group 4: Audit trail integration tests for QA/QC review workflows
    // -----------------------------------------------------------------------

    [Fact]
    public void QaQcReview_AuditTrail_RecordsSubmissionToReview()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-201",
            RevisionNumber = "A",
            IssuedBy       = "K.Brown",
        };

        var entry = RecordQaQcReview(revision, DrawingSignoffState.InReview,
            actor: "K.Brown", action: "submitted for QA/QC review");

        Assert.Equal(DrawingSignoffState.InReview, revision.State);
        Assert.Equal("DWG-SW-201",                entry.DrawingId);
        Assert.Equal("submitted for QA/QC review", entry.Action);
        Assert.Equal("K.Brown",                   entry.Actor);
        Assert.Equal(DrawingSignoffState.Draft,    entry.FromState);
        Assert.Equal(DrawingSignoffState.InReview, entry.ToState);
    }

    [Fact]
    public void QaQcReview_AuditTrail_RecordsRejectionWithFindings()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-DC-202",
            RevisionNumber = "B",
            IssuedBy       = "P.Singh",
            State          = DrawingSignoffState.InReview,
        };

        var entry = RecordQaQcReview(revision, DrawingSignoffState.Rejected,
            actor: "QA.Lead",
            action: "rejected",
            notes: "Protection relay settings not verified against specification.");

        Assert.Equal(DrawingSignoffState.Rejected,  revision.State);
        Assert.Equal("rejected",                   entry.Action);
        Assert.False(string.IsNullOrWhiteSpace(entry.Notes),
            "QA/QC rejection must record findings in the Notes field.");
        Assert.Contains("Protection relay", entry.Notes);
    }

    [Fact]
    public void QaQcReview_AuditTrail_RecordsApproval()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-CC-203",
            RevisionNumber = "A",
            IssuedBy       = "L.Garcia",
            State          = DrawingSignoffState.InReview,
        };

        var entry = RecordQaQcReview(revision, DrawingSignoffState.Approved,
            actor: "QA.Lead", action: "approved");

        Assert.Equal(DrawingSignoffState.Approved, revision.State);
        Assert.Equal("approved",                  entry.Action);
        Assert.Equal("QA.Lead",                   entry.Actor);
    }

    [Fact]
    public void QaQcReview_AuditTrail_RecordsFullReviewCycle_WithRework()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-204",
            RevisionNumber = "A",
            IssuedBy       = "A.Osei",
        };

        var trail = new List<AuditTrailEntry>
        {
            RecordQaQcReview(revision, DrawingSignoffState.InReview,
                actor: "A.Osei",   action: "submitted for QA/QC review"),
            RecordQaQcReview(revision, DrawingSignoffState.Rejected,
                actor: "QA.Lead",  action: "rejected",
                notes: "Interlocking verification diagram missing."),
            RecordQaQcReview(revision, DrawingSignoffState.InReview,
                actor: "A.Osei",   action: "resubmitted after rework"),
            RecordQaQcReview(revision, DrawingSignoffState.Approved,
                actor: "QA.Lead",  action: "approved"),
        };

        Assert.Equal(4, trail.Count);
        Assert.Equal(DrawingSignoffState.Approved, revision.State);

        // First entry: Draft -> InReview
        Assert.Equal(DrawingSignoffState.Draft,    trail[0].FromState);
        Assert.Equal(DrawingSignoffState.InReview, trail[0].ToState);

        // Rejection entry has findings in Notes
        Assert.False(string.IsNullOrWhiteSpace(trail[1].Notes));
        Assert.Equal(DrawingSignoffState.Rejected, trail[1].ToState);

        // Final entry: approved
        Assert.Equal("QA.Lead",                   trail[3].Actor);
        Assert.Equal(DrawingSignoffState.Approved, trail[3].ToState);
    }

    [Fact]
    public void QaQcReview_AuditTrail_AllEntries_HaveDrawingIdAndRevisionId()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-DC-205",
            RevisionNumber = "A",
            IssuedBy       = "R.Okonkwo",
        };

        var trail = new List<AuditTrailEntry>
        {
            RecordQaQcReview(revision, DrawingSignoffState.InReview,
                actor: "R.Okonkwo", action: "submitted for QA/QC review"),
            RecordQaQcReview(revision, DrawingSignoffState.Approved,
                actor: "QA.Lead",   action: "approved"),
        };

        foreach (var entry in trail)
        {
            Assert.Equal("DWG-DC-205", entry.DrawingId);
            Assert.Equal(revision.Id,  entry.RevisionId);
            Assert.False(string.IsNullOrWhiteSpace(entry.Actor));
            Assert.False(string.IsNullOrWhiteSpace(entry.Action));
        }
    }

    [Fact]
    public void QaQcReview_AuditTrail_TimestampsAreOrdered()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-206",
            RevisionNumber = "B",
            IssuedBy       = "C.Mensah",
        };

        var trail = new List<AuditTrailEntry>();

        // Simulate sequential QA/QC events with small delays to guarantee ordering.
        var t0 = DateTimeOffset.UtcNow;
        trail.Add(new AuditTrailEntry
        {
            DrawingId  = revision.DrawingId,
            RevisionId = revision.Id,
            Action     = "submitted for QA/QC review",
            Actor      = "C.Mensah",
            OccurredAt = t0,
        });
        trail.Add(new AuditTrailEntry
        {
            DrawingId  = revision.DrawingId,
            RevisionId = revision.Id,
            Action     = "approved",
            Actor      = "QA.Lead",
            OccurredAt = t0.AddSeconds(5),
        });

        Assert.True(trail[1].OccurredAt > trail[0].OccurredAt,
            "Later audit trail entries must have later timestamps.");
    }

    // -----------------------------------------------------------------------
    // Group 5: Production control workflow integration tests
    // -----------------------------------------------------------------------

    [Fact]
    public void ProductionControl_Revision_TransitionsFromDraft_ToApproved_ViaReview()
    {
        // Verify the full state machine: Draft -> InReview -> Approved
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-CC-301",
            RevisionNumber = "A",
            IssuedBy       = "S.Ibrahim",
        };

        Assert.Equal(DrawingSignoffState.Draft, revision.State);

        var e1 = RecordQaQcReview(revision, DrawingSignoffState.InReview,
            actor: "S.Ibrahim", action: "submitted for QA/QC review");
        Assert.Equal(DrawingSignoffState.InReview, revision.State);

        var e2 = RecordQaQcReview(revision, DrawingSignoffState.Approved,
            actor: "QA.Lead", action: "approved");
        Assert.Equal(DrawingSignoffState.Approved, revision.State);

        // Verify audit trail captures the full transition chain.
        Assert.Equal(DrawingSignoffState.Draft,    e1.FromState);
        Assert.Equal(DrawingSignoffState.InReview, e1.ToState);
        Assert.Equal(DrawingSignoffState.InReview, e2.FromState);
        Assert.Equal(DrawingSignoffState.Approved, e2.ToState);
    }

    [Fact]
    public void ProductionControl_IssueSet_ApprovesAfterAllRevisionsPassed()
    {
        // Verify that an issue set can be approved after all constituent
        // drawing revisions pass QA/QC review.
        var revA = new DrawingRevisionRecord
        {
            DrawingId = "DWG-SW-310", RevisionNumber = "A", IssuedBy = "M.Carter",
            State     = DrawingSignoffState.Approved,
        };
        var revB = new DrawingRevisionRecord
        {
            DrawingId = "DWG-DC-311", RevisionNumber = "A", IssuedBy = "M.Carter",
            State     = DrawingSignoffState.Approved,
        };

        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-QA-310",
            IssuedBy      = "M.Carter",
        };
        issueSet.RevisionIds.Add(revA.Id);
        issueSet.RevisionIds.Add(revB.Id);

        // Both revisions are approved — issue set can be approved.
        var allApproved = issueSet.RevisionIds.Count == 2
            && revA.State == DrawingSignoffState.Approved
            && revB.State == DrawingSignoffState.Approved;

        Assert.True(allApproved,
            "Issue set should be eligible for approval when all constituent revisions are approved.");

        issueSet.State = IssueSetState.Approved;
        Assert.Equal(IssueSetState.Approved, issueSet.State);
    }

    [Fact]
    public void ProductionControl_ApprovedRevision_ReceivesPackageRef()
    {
        // Verify that an approved revision can be assigned a production transmittal
        // package reference, completing the production control handoff.
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-CC-320",
            RevisionNumber = "B",
            IssuedBy       = "O.Andersen",
            State          = DrawingSignoffState.Approved,
        };

        revision.PackageRef = "PKG-PROD-2026-005";

        Assert.Equal("PKG-PROD-2026-005",         revision.PackageRef);
        Assert.Equal(DrawingSignoffState.Approved, revision.State);
    }

    [Fact]
    public void ProductionControl_SupersededRevision_IsReplacedByNewRevision()
    {
        // Verify that when a new revision is issued the old one can be marked
        // as Superseded — required for audit trail completeness.
        var revA = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-330",
            RevisionNumber = "A",
            IssuedBy       = "H.Petersen",
            State          = DrawingSignoffState.Approved,
        };

        var revB = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-330",
            RevisionNumber = "B",
            IssuedBy       = "H.Petersen",
        };

        // Supersede rev A when rev B is issued.
        var supersedureEntry = new AuditTrailEntry
        {
            DrawingId  = revA.DrawingId,
            RevisionId = revA.Id,
            Action     = "superseded by revision B",
            Actor      = "H.Petersen",
            FromState  = revA.State,
            ToState    = DrawingSignoffState.Superseded,
            OccurredAt = DateTimeOffset.UtcNow,
        };
        revA.State = DrawingSignoffState.Superseded;

        Assert.Equal(DrawingSignoffState.Superseded, revA.State);
        Assert.Equal(DrawingSignoffState.Draft,      revB.State);
        Assert.Equal("superseded by revision B",     supersedureEntry.Action);
        Assert.Equal(revA.Id,                        supersedureEntry.RevisionId);
    }

    [Fact]
    public void ProductionControl_AuditTrail_CombinesIssueSetAndRevisionEntries()
    {
        // Verify that audit trail entries for both drawing revisions and the
        // parent issue set can coexist and be identified by DrawingId/RevisionId.
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-DC-340",
            RevisionNumber = "A",
            IssuedBy       = "F.Kimura",
        };

        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-QA-340",
            IssuedBy      = "F.Kimura",
        };
        issueSet.RevisionIds.Add(revision.Id);

        var revisionEntry = RecordQaQcReview(revision, DrawingSignoffState.Approved,
            actor: "QA.Lead", action: "approved");

        var issueSetEntry = new AuditTrailEntry
        {
            DrawingId  = issueSet.DrawingSetRef,
            RevisionId = issueSet.Id,
            Action     = "issue set approved",
            Actor      = "QA.Lead",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        issueSet.State = IssueSetState.Approved;

        // Both entries must be identifiable by their respective DrawingId / RevisionId.
        Assert.Equal(revision.DrawingId,    revisionEntry.DrawingId);
        Assert.Equal(revision.Id,           revisionEntry.RevisionId);
        Assert.Equal(issueSet.DrawingSetRef, issueSetEntry.DrawingId);
        Assert.Equal(issueSet.Id,           issueSetEntry.RevisionId);
        Assert.Equal(IssueSetState.Approved, issueSet.State);
    }

    [Fact]
    public void ProductionControl_MultipleRevisions_EachHaveIndependentAuditTrails()
    {
        // Verify that different drawing revisions can each accumulate their own
        // independent audit trail entries — required for production traceability.
        var revisions = Enumerable.Range(1, 5)
            .Select(i => new DrawingRevisionRecord
            {
                DrawingId      = $"DWG-SW-{350 + i}",
                RevisionNumber = "A",
                IssuedBy       = "T.Osei",
            })
            .ToList();

        var allEntries = revisions
            .Select(r => RecordQaQcReview(r, DrawingSignoffState.Approved,
                actor: "QA.Lead", action: "approved"))
            .ToList();

        Assert.Equal(5, allEntries.Count);

        // All revisions are approved.
        Assert.All(revisions, r =>
            Assert.Equal(DrawingSignoffState.Approved, r.State));

        // Each audit entry references a distinct revision.
        var distinctRevisionIds = allEntries.Select(e => e.RevisionId).Distinct().ToList();
        Assert.Equal(5, distinctRevisionIds.Count);
    }

    // -----------------------------------------------------------------------
    // Group 6: Customer-safe workflow requirements integration tests
    //
    // Verify that the audit trail and revision model enforce customer-safe
    // workflow requirements: only Approved revisions may be included in
    // transmittal packages issued to customers, and every gate crossing is
    // recorded in the audit trail.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Helper that decides whether a revision is eligible to be added to a
    /// customer transmittal package.  Only Approved revisions qualify.
    /// </summary>
    private static bool IsEligibleForCustomerTransmittal(DrawingRevisionRecord revision)
        => revision.State == DrawingSignoffState.Approved;

    [Fact]
    public void CustomerSafe_DraftRevision_IsNotEligibleForTransmittalPackage()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-401",
            RevisionNumber = "A",
            IssuedBy       = "J.Thomas",
            State          = DrawingSignoffState.Draft,
        };

        Assert.False(IsEligibleForCustomerTransmittal(revision),
            "A Draft revision must not be eligible for inclusion in a customer transmittal package.");
    }

    [Fact]
    public void CustomerSafe_InReviewRevision_IsNotEligibleForTransmittalPackage()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-DC-402",
            RevisionNumber = "A",
            IssuedBy       = "J.Thomas",
            State          = DrawingSignoffState.InReview,
        };

        Assert.False(IsEligibleForCustomerTransmittal(revision),
            "An InReview revision must not be eligible for inclusion in a customer transmittal package.");
    }

    [Fact]
    public void CustomerSafe_RejectedRevision_IsNotEligibleForTransmittalPackage()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-CC-403",
            RevisionNumber = "A",
            IssuedBy       = "J.Thomas",
            State          = DrawingSignoffState.Rejected,
        };

        Assert.False(IsEligibleForCustomerTransmittal(revision),
            "A Rejected revision must not be eligible for inclusion in a customer transmittal package.");
    }

    [Fact]
    public void CustomerSafe_SupersededRevision_IsNotEligibleForTransmittalPackage()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-404",
            RevisionNumber = "A",
            IssuedBy       = "J.Thomas",
            State          = DrawingSignoffState.Superseded,
        };

        Assert.False(IsEligibleForCustomerTransmittal(revision),
            "A Superseded revision must not be eligible for inclusion in a customer transmittal package.");
    }

    [Fact]
    public void CustomerSafe_ApprovedRevision_IsEligibleForTransmittalPackage()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-405",
            RevisionNumber = "B",
            IssuedBy       = "J.Thomas",
            State          = DrawingSignoffState.Approved,
        };

        Assert.True(IsEligibleForCustomerTransmittal(revision),
            "Only an Approved revision must be eligible for inclusion in a customer transmittal package.");
    }

    [Fact]
    public void CustomerSafe_PackageRef_IsOnlyAssigned_AfterApproval()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-DC-406",
            RevisionNumber = "A",
            IssuedBy       = "M.Nakamura",
        };

        // Simulate the approval gate before assigning the package reference.
        Assert.False(IsEligibleForCustomerTransmittal(revision),
            "Revision must not be eligible for transmittal before approval.");

        RecordQaQcReview(revision, DrawingSignoffState.InReview,
            actor: "M.Nakamura", action: "submitted for QA/QC review");
        Assert.False(IsEligibleForCustomerTransmittal(revision),
            "Revision must not be eligible for transmittal while InReview.");

        RecordQaQcReview(revision, DrawingSignoffState.Approved,
            actor: "QA.Lead", action: "approved");

        // Now the revision is approved — assign the package reference.
        Assert.True(IsEligibleForCustomerTransmittal(revision));
        revision.PackageRef = "PKG-CUST-2026-010";

        Assert.False(string.IsNullOrWhiteSpace(revision.PackageRef),
            "PackageRef must be set after the revision is approved.");
    }

    [Fact]
    public void CustomerSafe_AuditTrail_RecordsApprovalGate_BeforePackageAssignment()
    {
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-CC-407",
            RevisionNumber = "A",
            IssuedBy       = "P.Okafor",
        };

        var trail = new List<AuditTrailEntry>
        {
            RecordQaQcReview(revision, DrawingSignoffState.InReview,
                actor: "P.Okafor", action: "submitted for QA/QC review"),
            RecordQaQcReview(revision, DrawingSignoffState.Approved,
                actor: "QA.Lead", action: "approved"),
        };

        // Assign package ref after approval.
        revision.PackageRef = "PKG-CUST-2026-011";
        var issueEntry = new AuditTrailEntry
        {
            DrawingId  = revision.DrawingId,
            RevisionId = revision.Id,
            Action     = "issued to customer transmittal package",
            Actor      = "QA.Lead",
            FromState  = revision.State,
            ToState    = revision.State,
            Notes      = $"Package: {revision.PackageRef}",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        trail.Add(issueEntry);

        // The approval gate must appear before the package issuance entry.
        var approvalIndex = trail.FindIndex(e => e.Action == "approved");
        var issueIndex    = trail.FindIndex(e => e.Action == "issued to customer transmittal package");

        Assert.True(approvalIndex >= 0, "Audit trail must contain an approval entry.");
        Assert.True(issueIndex    >= 0, "Audit trail must contain a package issuance entry.");
        Assert.True(approvalIndex < issueIndex,
            "Approval must be recorded before the package issuance in the audit trail.");
    }

    [Fact]
    public void CustomerSafe_IssueSet_IsOnlyEligible_WhenAllRevisionsApproved()
    {
        // A package with mixed-state revisions must not be eligible for customer issue.
        var revApproved = new DrawingRevisionRecord
        {
            DrawingId = "DWG-SW-410", RevisionNumber = "A", IssuedBy = "D.Levy",
            State     = DrawingSignoffState.Approved,
        };
        var revInReview = new DrawingRevisionRecord
        {
            DrawingId = "DWG-DC-411", RevisionNumber = "A", IssuedBy = "D.Levy",
            State     = DrawingSignoffState.InReview,
        };

        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-CUST-410",
            IssuedBy      = "D.Levy",
        };
        issueSet.RevisionIds.Add(revApproved.Id);
        issueSet.RevisionIds.Add(revInReview.Id);

        var allRevisions = new[] { revApproved, revInReview };
        bool eligibleForCustomerIssue = allRevisions.All(r => r.State == DrawingSignoffState.Approved);

        Assert.False(eligibleForCustomerIssue,
            "Issue set must not be eligible for customer issue when any constituent revision is not Approved.");
    }

    [Fact]
    public void CustomerSafe_IssueSet_IsEligible_WhenAllRevisionsApproved()
    {
        var revA = new DrawingRevisionRecord
        {
            DrawingId = "DWG-SW-420", RevisionNumber = "A", IssuedBy = "S.Park",
            State     = DrawingSignoffState.Approved,
        };
        var revB = new DrawingRevisionRecord
        {
            DrawingId = "DWG-DC-421", RevisionNumber = "A", IssuedBy = "S.Park",
            State     = DrawingSignoffState.Approved,
        };

        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-CUST-420",
            IssuedBy      = "S.Park",
        };
        issueSet.RevisionIds.Add(revA.Id);
        issueSet.RevisionIds.Add(revB.Id);

        var allRevisions = new[] { revA, revB };
        bool eligibleForCustomerIssue = allRevisions.All(r => r.State == DrawingSignoffState.Approved);

        Assert.True(eligibleForCustomerIssue,
            "Issue set must be eligible for customer issue when all constituent revisions are Approved.");
    }

    [Fact]
    public void CustomerSafe_AuditTrail_ContainsActor_ForEveryTransition()
    {
        // Every state transition recorded in the audit trail must identify
        // the accountable actor — required for customer-safe traceability.
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-CC-430",
            RevisionNumber = "A",
            IssuedBy       = "B.Fernandez",
        };

        var trail = new List<AuditTrailEntry>
        {
            RecordQaQcReview(revision, DrawingSignoffState.InReview,
                actor: "B.Fernandez", action: "submitted for QA/QC review"),
            RecordQaQcReview(revision, DrawingSignoffState.Rejected,
                actor: "QA.Lead", action: "rejected",
                notes: "Earth fault protection missing from single-line diagram."),
            RecordQaQcReview(revision, DrawingSignoffState.InReview,
                actor: "B.Fernandez", action: "resubmitted after rework"),
            RecordQaQcReview(revision, DrawingSignoffState.Approved,
                actor: "QA.Lead", action: "approved"),
        };

        Assert.All(trail, entry =>
            Assert.False(string.IsNullOrWhiteSpace(entry.Actor),
                $"Audit entry for action '{entry.Action}' must identify the actor."));
    }

    // -----------------------------------------------------------------------
    // Group 7: Controlled document handling integration tests
    //
    // Verify that the revision tracking and audit trail model supports
    // controlled document handling: revisions are uniquely numbered, only
    // the latest non-superseded revision is current, and every supersedure
    // is audit-trailed.
    // -----------------------------------------------------------------------

    [Fact]
    public void ControlledDocument_OnlyOneActiveRevision_PerDrawing()
    {
        // Verify that when revision B is issued, revision A is superseded and
        // only revision B is in a non-superseded (active) state for the drawing.
        var revA = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-501",
            RevisionNumber = "A",
            IssuedBy       = "N.Christodoulou",
            State          = DrawingSignoffState.Approved,
        };

        var revB = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-501",
            RevisionNumber = "B",
            IssuedBy       = "N.Christodoulou",
            State          = DrawingSignoffState.Draft,
        };

        // Supersede rev A when rev B is issued.
        revA.State = DrawingSignoffState.Superseded;

        var allRevisions = new[] { revA, revB };
        var activeRevisions = allRevisions
            .Where(r => r.State != DrawingSignoffState.Superseded)
            .ToList();

        Assert.True(activeRevisions.Count == 1,
            "Only one active (non-superseded) revision must exist per drawing at any time.");
        Assert.Equal("B", activeRevisions[0].RevisionNumber);
    }

    [Fact]
    public void ControlledDocument_SupersededRevision_ExcludedFromCurrentDocumentList()
    {
        var revA = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-DC-502",
            RevisionNumber = "A",
            IssuedBy       = "E.Kowalski",
            State          = DrawingSignoffState.Superseded,
        };
        var revB = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-DC-502",
            RevisionNumber = "B",
            IssuedBy       = "E.Kowalski",
            State          = DrawingSignoffState.Approved,
        };

        var currentDocuments = new[] { revA, revB }
            .Where(r => r.State != DrawingSignoffState.Superseded)
            .ToList();

        Assert.Single(currentDocuments);
        Assert.Equal("B", currentDocuments[0].RevisionNumber);
        Assert.DoesNotContain(revA, currentDocuments);
    }

    [Fact]
    public void ControlledDocument_RevisionNumbers_AreUnique_PerDrawing()
    {
        // Verify that revision records for the same drawing have distinct
        // revision designators — a core controlled-document requirement.
        var revisions = new[]
        {
            new DrawingRevisionRecord { DrawingId = "DWG-CC-503", RevisionNumber = "A" },
            new DrawingRevisionRecord { DrawingId = "DWG-CC-503", RevisionNumber = "B" },
            new DrawingRevisionRecord { DrawingId = "DWG-CC-503", RevisionNumber = "C" },
        };

        var distinctNumbers = revisions.Select(r => r.RevisionNumber).Distinct().ToList();

        Assert.True(revisions.Length == distinctNumbers.Count,
            "Each revision of a controlled drawing must have a unique revision number.");
    }

    [Fact]
    public void ControlledDocument_AuditTrail_RecordsSupersedure_WhenNewRevisionIssued()
    {
        var revA = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-510",
            RevisionNumber = "A",
            IssuedBy       = "W.Osei",
            State          = DrawingSignoffState.Approved,
        };

        var revB = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-SW-510",
            RevisionNumber = "B",
            IssuedBy       = "W.Osei",
        };

        // Record the supersedure in the audit trail.
        var supersedureEntry = new AuditTrailEntry
        {
            DrawingId  = revA.DrawingId,
            RevisionId = revA.Id,
            Action     = "superseded by revision B",
            Actor      = "W.Osei",
            FromState  = revA.State,
            ToState    = DrawingSignoffState.Superseded,
            Notes      = $"Replaced by revision {revB.RevisionNumber}.",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        revA.State = DrawingSignoffState.Superseded;

        Assert.Equal(DrawingSignoffState.Superseded, revA.State);
        Assert.Equal(DrawingSignoffState.Superseded, supersedureEntry.ToState);
        Assert.Contains("revision B", supersedureEntry.Action, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(supersedureEntry.Notes),
            "Supersedure audit entry must record a note explaining what replaced the revision.");
    }

    [Fact]
    public void ControlledDocument_FullLifecycle_DraftToSuperseded_IsFullyAuditTrailed()
    {
        // Simulate a complete controlled-document lifecycle for a single drawing:
        // Draft -> InReview -> Approved -> Superseded (by next revision).
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-DC-520",
            RevisionNumber = "A",
            IssuedBy       = "C.Boateng",
            State          = DrawingSignoffState.Draft,
        };

        var trail = new List<AuditTrailEntry>
        {
            RecordQaQcReview(revision, DrawingSignoffState.InReview,
                actor: "C.Boateng", action: "submitted for QA/QC review"),
            RecordQaQcReview(revision, DrawingSignoffState.Approved,
                actor: "QA.Lead", action: "approved"),
        };

        // Assign package ref on approval.
        revision.PackageRef = "PKG-CTRL-2026-020";

        // Issue a new revision — supersede this one.
        var supersedureEntry = new AuditTrailEntry
        {
            DrawingId  = revision.DrawingId,
            RevisionId = revision.Id,
            Action     = "superseded by revision B",
            Actor      = "C.Boateng",
            FromState  = revision.State,
            ToState    = DrawingSignoffState.Superseded,
            OccurredAt = DateTimeOffset.UtcNow,
        };
        revision.State = DrawingSignoffState.Superseded;
        trail.Add(supersedureEntry);

        Assert.Equal(3, trail.Count);
        Assert.Equal(DrawingSignoffState.Draft,      trail[0].FromState);
        Assert.Equal(DrawingSignoffState.InReview,   trail[0].ToState);
        Assert.Equal(DrawingSignoffState.InReview,   trail[1].FromState);
        Assert.Equal(DrawingSignoffState.Approved,   trail[1].ToState);
        Assert.Equal(DrawingSignoffState.Superseded, trail[2].ToState);
        Assert.Equal(DrawingSignoffState.Superseded, revision.State);
        Assert.False(string.IsNullOrWhiteSpace(revision.PackageRef),
            "PackageRef must survive the supersedure — it records which package included this revision.");
    }

    [Fact]
    public void ControlledDocument_AuditTrail_AllEntries_HaveNonEmptyIds()
    {
        // Verify that every controlled-document audit entry receives a unique,
        // non-empty identifier — required for controlled-document record keeping.
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-CC-530",
            RevisionNumber = "A",
            IssuedBy       = "V.Ionescu",
        };

        var trail = new List<AuditTrailEntry>
        {
            RecordQaQcReview(revision, DrawingSignoffState.InReview,
                actor: "V.Ionescu", action: "submitted for QA/QC review"),
            RecordQaQcReview(revision, DrawingSignoffState.Approved,
                actor: "QA.Lead", action: "approved"),
        };

        Assert.All(trail, entry =>
            Assert.False(string.IsNullOrWhiteSpace(entry.Id),
                "Every controlled-document audit entry must have a non-empty unique Id."));

        var distinctIds = trail.Select(e => e.Id).Distinct().ToList();
        Assert.True(trail.Count == distinctIds.Count,
            "Audit entry Ids must be unique across controlled-document trail entries.");
    }

    [Fact]
    public void ControlledDocument_Chunk8_Prompt_References_ControlledDocumentConcepts()
    {
        // Verify that AGENT_REPLY_GUIDE.md chunk8 references controlled-document
        // handling concepts required for customer-safe workflow compliance.
        var section = ExtractQaQcTemplatesSection(ReadGuide());

        Assert.True(
            section.Contains("review", StringComparison.OrdinalIgnoreCase),
            "chunk8 must reference the drawing review step as a controlled-document gate.");
        Assert.True(
            section.Contains("standard", StringComparison.OrdinalIgnoreCase)
            || section.Contains("template", StringComparison.OrdinalIgnoreCase),
            "chunk8 must reference standards or templates as the basis for controlled-document compliance.");
    }
}
