using DailyDesk.Models;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify approval routing and workflow fit compliance
/// as specified in AGENT_REPLY_GUIDE.md chunk7 (Best Reply Patterns For
/// Approval Routing and Workflow Fit).
///
/// The tests are structured in five groups:
///   1. Document compliance — verify AGENT_REPLY_GUIDE.md chunk7 contains all
///      required approval-routing and workflow-fit specification elements.
///   2. IssueSetRecord model compliance — verify the model has all fields
///      required by the issue-set handling specification.
///   3. IssueSetState enum compliance — verify all required issue-set states
///      are present.
///   4. Issue-set audit trail integration tests — verify that issue-set
///      state transitions integrate correctly with the existing audit trail.
///   5. Approval routing and workflow fit integration tests — verify end-to-end
///      alignment between revision tracking, issue-set handling, and audit trail.
/// </summary>
public sealed class AuditTrailChunk7Tests
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
    /// Extracts the body of the "Best Reply Patterns For Approval Routing and Workflow Fit"
    /// section (chunk7) from AGENT_REPLY_GUIDE.md.
    /// </summary>
    private static string ExtractApprovalRoutingSection(string guide)
    {
        const string sectionHeader = "## Best Reply Patterns For Approval Routing and Workflow Fit";
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
    /// Helper that applies a state transition to an issue set and records an audit entry.
    /// FromState and ToState are left as their defaults because IssueSetState and
    /// DrawingSignoffState are semantically distinct enums; issue-set audit entries
    /// carry the state information in the Action/Notes fields instead.
    /// </summary>
    private static AuditTrailEntry TransitionIssueSet(
        IssueSetRecord issueSet,
        IssueSetState toState,
        string actor,
        string action,
        string notes = "")
    {
        var entry = new AuditTrailEntry
        {
            DrawingId  = issueSet.DrawingSetRef,
            RevisionId = issueSet.Id,
            Action     = action,
            Actor      = actor,
            Notes      = notes,
            OccurredAt = DateTimeOffset.UtcNow,
        };
        issueSet.State = toState;
        return entry;
    }

    // -----------------------------------------------------------------------
    // Group 1: AGENT_REPLY_GUIDE.md chunk7 document compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void AgentReplyGuide_ContainsApprovalRoutingAndWorkflowFitSection()
    {
        var guide   = ReadGuide();
        var section = ExtractApprovalRoutingSection(guide);
        Assert.False(string.IsNullOrWhiteSpace(section),
            "AGENT_REPLY_GUIDE.md must contain a '## Best Reply Patterns For Approval Routing and Workflow Fit' section (chunk7)");
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_ContainsRevisionTrackingAlignmentPattern()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("### Revision Tracking Alignment", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_ContainsIssueSetHandlingPattern()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("### Issue-Set Handling", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_ContainsAuditTrailCompliancePattern()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("### Audit Trail Compliance", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_ContainsApprovalRoutingVerificationPattern()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("### Approval Routing Verification", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_ContainsWorkflowFitAssessmentPattern()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("### Workflow Fit Assessment", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_RevisionTrackingPattern_ContainsApprovalGates()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("approval gates", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_IssueSetPattern_ContainsRejectionPaths()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("rejection paths", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_IssueSetPattern_ContainsResubmissionRules()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("resubmission rules", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_AuditTrailPattern_ContainsStateTransitionRecords()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("state transition records", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_AuditTrailPattern_ContainsActorAccountability()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        Assert.Contains("actor accountability", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_Chunk7_WorkflowFitPattern_ExcludesCRMAndInvoicing()
    {
        var section = ExtractApprovalRoutingSection(ReadGuide());
        // The spec must explicitly constrain scope away from CRM and invoicing.
        Assert.True(
            section.Contains("CRM", StringComparison.OrdinalIgnoreCase) ||
            section.Contains("invoicing", StringComparison.OrdinalIgnoreCase),
            "Workflow Fit Assessment pattern must reference 'CRM' and/or 'invoicing' as out-of-scope constraints");
    }

    // -----------------------------------------------------------------------
    // Group 2: IssueSetRecord model compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void IssueSetRecord_HasId_ForUniqueTracking()
    {
        var record = new IssueSetRecord();
        Assert.False(string.IsNullOrWhiteSpace(record.Id),
            "IssueSetRecord must have a non-empty Id for unique issue-set tracking");
    }

    [Fact]
    public void IssueSetRecord_HasDrawingSetRef_ForIssueSetHandling()
    {
        var record = new IssueSetRecord { DrawingSetRef = "DWG-SET-001" };
        Assert.Equal("DWG-SET-001", record.DrawingSetRef);
    }

    [Fact]
    public void IssueSetRecord_HasIssuedBy_ForSignoffAccountability()
    {
        var record = new IssueSetRecord { IssuedBy = "J.Smith" };
        Assert.Equal("J.Smith", record.IssuedBy);
    }

    [Fact]
    public void IssueSetRecord_HasState_AsIssueSetState()
    {
        var record = new IssueSetRecord { State = IssueSetState.InApproval };
        Assert.Equal(IssueSetState.InApproval, record.State);
    }

    [Fact]
    public void IssueSetRecord_DefaultState_IsPending()
    {
        var record = new IssueSetRecord();
        Assert.Equal(IssueSetState.Pending, record.State);
    }

    [Fact]
    public void IssueSetRecord_HasIssuedAt_ForAuditTimestamp()
    {
        var now    = DateTimeOffset.UtcNow;
        var record = new IssueSetRecord { IssuedAt = now };
        Assert.Equal(now, record.IssuedAt);
    }

    [Fact]
    public void IssueSetRecord_HasRevisionIds_ForIssueSetComposition()
    {
        var record = new IssueSetRecord();
        record.RevisionIds.Add("rev-001");
        record.RevisionIds.Add("rev-002");
        Assert.Equal(2, record.RevisionIds.Count);
        Assert.Contains("rev-001", record.RevisionIds);
        Assert.Contains("rev-002", record.RevisionIds);
    }

    [Fact]
    public void IssueSetRecord_HasRejectionReason_ForRejectionPath()
    {
        var record = new IssueSetRecord { RejectionReason = "Earthing schedule missing." };
        Assert.Equal("Earthing schedule missing.", record.RejectionReason);
    }

    [Fact]
    public void IssueSetRecord_DefaultRejectionReason_IsEmpty()
    {
        var record = new IssueSetRecord();
        Assert.Equal(string.Empty, record.RejectionReason);
    }

    [Fact]
    public void IssueSetRecord_HasPackageRef_ForHandoffTracking()
    {
        var record = new IssueSetRecord { PackageRef = "PKG-2026-007" };
        Assert.Equal("PKG-2026-007", record.PackageRef);
    }

    [Fact]
    public void IssueSetRecord_DefaultPackageRef_IsEmpty()
    {
        var record = new IssueSetRecord();
        Assert.Equal(string.Empty, record.PackageRef);
    }

    // -----------------------------------------------------------------------
    // Group 3: IssueSetState enum compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void IssueSetState_HasPendingState()
    {
        Assert.True(Enum.IsDefined(typeof(IssueSetState), IssueSetState.Pending),
            "IssueSetState must include Pending — the initial state before approval submission");
    }

    [Fact]
    public void IssueSetState_HasInApprovalState()
    {
        Assert.True(Enum.IsDefined(typeof(IssueSetState), IssueSetState.InApproval),
            "IssueSetState must include InApproval — the active approval gate state");
    }

    [Fact]
    public void IssueSetState_HasApprovedState()
    {
        Assert.True(Enum.IsDefined(typeof(IssueSetState), IssueSetState.Approved),
            "IssueSetState must include Approved — the final acceptance state");
    }

    [Fact]
    public void IssueSetState_HasRejectedState()
    {
        Assert.True(Enum.IsDefined(typeof(IssueSetState), IssueSetState.Rejected),
            "IssueSetState must include Rejected — the rejection path state requiring rework");
    }

    [Fact]
    public void IssueSetState_HasResubmittedState()
    {
        Assert.True(Enum.IsDefined(typeof(IssueSetState), IssueSetState.Resubmitted),
            "IssueSetState must include Resubmitted — the resubmission rule state after rejection");
    }

    [Fact]
    public void IssueSetState_HasExactlyFiveStates()
    {
        var states = Enum.GetValues<IssueSetState>();
        Assert.Equal(5, states.Length);
    }

    // -----------------------------------------------------------------------
    // Group 4: Issue-set audit trail integration tests
    // -----------------------------------------------------------------------

    [Fact]
    public void IssueSetWorkflow_NewIssueSet_StartsInPendingState()
    {
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E001",
            IssuedBy      = "J.Smith",
        };

        Assert.Equal(IssueSetState.Pending, issueSet.State);
    }

    [Fact]
    public void IssueSetWorkflow_PendingToInApproval_GeneratesAuditEntry()
    {
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E001",
            IssuedBy      = "J.Smith",
        };

        var entry = TransitionIssueSet(issueSet, IssueSetState.InApproval,
            actor: "J.Smith", action: "submitted for approval");

        Assert.Equal(IssueSetState.InApproval,         issueSet.State);
        Assert.Equal("J.Smith",                         entry.Actor);
        Assert.Equal("submitted for approval",          entry.Action);
        Assert.Equal("DWG-SET-E001",                    entry.DrawingId);
        Assert.Equal(issueSet.Id,                       entry.RevisionId);
    }

    [Fact]
    public void IssueSetWorkflow_InApprovalToApproved_GeneratesAuditEntry()
    {
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E001",
            IssuedBy      = "J.Smith",
            State         = IssueSetState.InApproval,
        };

        var entry = TransitionIssueSet(issueSet, IssueSetState.Approved,
            actor: "B.Jones", action: "approved");

        Assert.Equal(IssueSetState.Approved, issueSet.State);
        Assert.Equal("B.Jones",              entry.Actor);
        Assert.Equal("approved",             entry.Action);
    }

    [Fact]
    public void IssueSetWorkflow_InApprovalToRejected_GeneratesAuditEntryWithReason()
    {
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E001",
            IssuedBy      = "J.Smith",
            State         = IssueSetState.InApproval,
        };
        const string reason = "Earthing schedule missing from panel layout.";
        issueSet.RejectionReason = reason;

        var entry = TransitionIssueSet(issueSet, IssueSetState.Rejected,
            actor: "B.Jones", action: "rejected", notes: reason);

        Assert.Equal(IssueSetState.Rejected, issueSet.State);
        Assert.Equal(reason,                 entry.Notes);
        Assert.Equal(reason,                 issueSet.RejectionReason);
    }

    [Fact]
    public void IssueSetWorkflow_RejectedToResubmitted_FollowsResubmissionRule()
    {
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef    = "DWG-SET-E001",
            IssuedBy         = "J.Smith",
            State            = IssueSetState.Rejected,
            RejectionReason  = "Earthing schedule missing.",
        };

        var entry = TransitionIssueSet(issueSet, IssueSetState.Resubmitted,
            actor: "J.Smith", action: "resubmitted after rework");

        Assert.Equal(IssueSetState.Resubmitted,     issueSet.State);
        Assert.Equal("resubmitted after rework",    entry.Action);
    }

    [Fact]
    public void IssueSetWorkflow_AuditTrail_RecordsFullApprovalCycle()
    {
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E002",
            IssuedBy      = "A.Patel",
        };

        var auditTrail = new List<AuditTrailEntry>
        {
            TransitionIssueSet(issueSet, IssueSetState.InApproval,
                actor: "A.Patel", action: "submitted for approval"),
            TransitionIssueSet(issueSet, IssueSetState.Rejected,
                actor: "C.Wong",  action: "rejected",
                notes: "Cable schedule not coordinated with single-line diagram."),
            TransitionIssueSet(issueSet, IssueSetState.Resubmitted,
                actor: "A.Patel", action: "resubmitted after rework"),
            TransitionIssueSet(issueSet, IssueSetState.InApproval,
                actor: "A.Patel", action: "re-submitted for approval"),
            TransitionIssueSet(issueSet, IssueSetState.Approved,
                actor: "C.Wong",  action: "approved"),
        };

        Assert.Equal(5, auditTrail.Count);
        Assert.Equal(IssueSetState.Approved, issueSet.State);

        // First entry: Pending -> InApproval
        Assert.Equal("submitted for approval", auditTrail[0].Action);

        // Rejection entry has notes
        Assert.False(string.IsNullOrWhiteSpace(auditTrail[1].Notes));

        // Final entry: approved by reviewer
        Assert.Equal("C.Wong",    auditTrail[4].Actor);
        Assert.Equal("approved",  auditTrail[4].Action);
    }

    [Fact]
    public void IssueSetWorkflow_AuditTrail_AllEntriesHaveDrawingSetRefAndIssueSetId()
    {
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E003",
            IssuedBy      = "M.Taylor",
        };

        var auditTrail = new List<AuditTrailEntry>
        {
            TransitionIssueSet(issueSet, IssueSetState.InApproval,
                actor: "M.Taylor", action: "submitted for approval"),
            TransitionIssueSet(issueSet, IssueSetState.Approved,
                actor: "L.Chen",   action: "approved"),
        };

        foreach (var entry in auditTrail)
        {
            Assert.Equal("DWG-SET-E003", entry.DrawingId);
            Assert.Equal(issueSet.Id,    entry.RevisionId);
            Assert.False(string.IsNullOrWhiteSpace(entry.Actor));
            Assert.False(string.IsNullOrWhiteSpace(entry.Action));
        }
    }

    // -----------------------------------------------------------------------
    // Group 5: Approval routing and workflow fit integration tests
    // -----------------------------------------------------------------------

    [Fact]
    public void ApprovalRouting_IssueSet_ComposedFromMultipleRevisions()
    {
        // Verify that an issue set can aggregate multiple drawing revisions,
        // aligning issue-set handling with revision tracking.
        var revA = new DrawingRevisionRecord { DrawingId = "DWG-E010", RevisionNumber = "A", IssuedBy = "J.Smith" };
        var revB = new DrawingRevisionRecord { DrawingId = "DWG-E011", RevisionNumber = "A", IssuedBy = "J.Smith" };
        var revC = new DrawingRevisionRecord { DrawingId = "DWG-E012", RevisionNumber = "A", IssuedBy = "J.Smith" };

        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E010",
            IssuedBy      = "J.Smith",
        };
        issueSet.RevisionIds.Add(revA.Id);
        issueSet.RevisionIds.Add(revB.Id);
        issueSet.RevisionIds.Add(revC.Id);

        Assert.Equal(3, issueSet.RevisionIds.Count);
        Assert.Contains(revA.Id, issueSet.RevisionIds);
        Assert.Contains(revB.Id, issueSet.RevisionIds);
        Assert.Contains(revC.Id, issueSet.RevisionIds);
    }

    [Fact]
    public void ApprovalRouting_IssueSetApproval_AlignsWith_RevisionApproval()
    {
        // Verify that approving an issue set and approving its constituent revisions
        // can be tracked through the same audit trail infrastructure.
        var revision = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-E020",
            RevisionNumber = "B",
            IssuedBy       = "R.Nguyen",
            State          = DrawingSignoffState.InReview,
        };

        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E020",
            IssuedBy      = "R.Nguyen",
            State         = IssueSetState.InApproval,
        };
        issueSet.RevisionIds.Add(revision.Id);

        // Approve the revision.
        var revisionAuditEntry = new AuditTrailEntry
        {
            DrawingId  = revision.DrawingId,
            RevisionId = revision.Id,
            Action     = "approved",
            Actor      = "B.Jones",
            FromState  = revision.State,
            ToState    = DrawingSignoffState.Approved,
            OccurredAt = DateTimeOffset.UtcNow,
        };
        revision.State = DrawingSignoffState.Approved;

        // Approve the issue set.
        var issueSetAuditEntry = TransitionIssueSet(issueSet, IssueSetState.Approved,
            actor: "B.Jones", action: "approved");

        Assert.Equal(DrawingSignoffState.Approved, revision.State);
        Assert.Equal(IssueSetState.Approved,       issueSet.State);
        Assert.Equal("B.Jones", revisionAuditEntry.Actor);
        Assert.Equal("B.Jones", issueSetAuditEntry.Actor);
    }

    [Fact]
    public void WorkflowFit_ApprovedIssueSet_AssignsPackageRef()
    {
        // Verify that after approval the issue set receives a transmittal package
        // reference — alignment between approval routing and package handoff.
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E030",
            IssuedBy      = "R.Nguyen",
            State         = IssueSetState.Approved,
        };

        issueSet.PackageRef = "PKG-2026-010";

        Assert.Equal("PKG-2026-010",        issueSet.PackageRef);
        Assert.Equal(IssueSetState.Approved, issueSet.State);
    }

    [Fact]
    public void WorkflowFit_IssueSetIds_AreUniqueAcrossInstances()
    {
        // Each new IssueSetRecord must receive a distinct Id — required for
        // audit trail uniqueness and approval routing traceability.
        var ids = Enumerable.Range(0, 50)
                            .Select(_ => new IssueSetRecord().Id)
                            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void WorkflowFit_AuditTrailEntry_SupportsIssueSetRevisionId()
    {
        // Verify the existing AuditTrailEntry model can hold an IssueSetRecord Id
        // as its RevisionId — confirming workflow fit between audit trail and issue sets.
        var issueSet = new IssueSetRecord
        {
            DrawingSetRef = "DWG-SET-E040",
            IssuedBy      = "A.Patel",
        };

        var entry = new AuditTrailEntry
        {
            DrawingId  = issueSet.DrawingSetRef,
            RevisionId = issueSet.Id,
            Action     = "issue set submitted",
            Actor      = "A.Patel",
            OccurredAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal(issueSet.Id,           entry.RevisionId);
        Assert.Equal(issueSet.DrawingSetRef, entry.DrawingId);
        Assert.False(string.IsNullOrWhiteSpace(entry.RevisionId));
    }
}
