using DailyDesk.Models;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify the electrical drafting production control and audit trail
/// implementation steps are documented in AGENT_REPLY_GUIDE.md (chunk8:
/// "Electrical Drafting Production Control: Implementation Steps").
///
/// The tests are structured in four groups:
///   1. Document compliance — verify AGENT_REPLY_GUIDE.md chunk8 contains all required
///      production control and audit trail implementation step elements.
///   2. DraftFlow integration section compliance — verify DraftFlow evaluation steps
///      and Suite model mapping are present.
///   3. Audit trail implementation section compliance — verify required audit fields,
///      state transition recording steps, and issue-set transition steps are documented.
///   4. Suite model reference compliance — verify the model reference table covers all
///      five core production control models.
/// </summary>
public sealed class DraftFlowProductionControlTests
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
    /// Extracts the body of the "Electrical Drafting Production Control: Implementation Steps"
    /// section (chunk8) from AGENT_REPLY_GUIDE.md.
    /// </summary>
    private static string ExtractProductionControlSection(string guide)
    {
        const string sectionHeader = "## Electrical Drafting Production Control: Implementation Steps";
        var start = guide.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var nextSection = guide.IndexOf("\n## ", start + sectionHeader.Length, StringComparison.Ordinal);
        return nextSection >= 0
            ? guide[start..nextSection]
            : guide[start..];
    }

    /// <summary>
    /// Extracts the body of the "DraftFlow Evaluation and Integration" sub-section.
    /// </summary>
    private static string ExtractDraftFlowSection(string section)
    {
        const string subHeader = "### DraftFlow Evaluation and Integration";
        var start = section.IndexOf(subHeader, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var next = section.IndexOf("\n### ", start + subHeader.Length, StringComparison.Ordinal);
        return next >= 0
            ? section[start..next]
            : section[start..];
    }

    /// <summary>
    /// Extracts the body of the "Audit Trail Implementation Steps" sub-section.
    /// </summary>
    private static string ExtractAuditTrailImplSection(string section)
    {
        const string subHeader = "### Audit Trail Implementation Steps";
        var start = section.IndexOf(subHeader, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var next = section.IndexOf("\n### ", start + subHeader.Length, StringComparison.Ordinal);
        return next >= 0
            ? section[start..next]
            : section[start..];
    }

    // -----------------------------------------------------------------------
    // Group 1: chunk8 document-level compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void AgentReplyGuide_ContainsProductionControlImplementationSection()
    {
        var guide   = ReadGuide();
        var section = ExtractProductionControlSection(guide);
        Assert.False(string.IsNullOrWhiteSpace(section),
            "AGENT_REPLY_GUIDE.md must contain an " +
            "'## Electrical Drafting Production Control: Implementation Steps' section (chunk8)");
    }

    [Fact]
    public void ProductionControlSection_ContainsDraftFlowEvaluationSubSection()
    {
        var section = ExtractProductionControlSection(ReadGuide());
        Assert.Contains("### DraftFlow Evaluation and Integration", section, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionControlSection_ContainsAuditTrailImplementationSubSection()
    {
        var section = ExtractProductionControlSection(ReadGuide());
        Assert.Contains("### Audit Trail Implementation Steps", section, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionControlSection_ContainsProductionControlWorkflowSubSection()
    {
        var section = ExtractProductionControlSection(ReadGuide());
        Assert.Contains("### Production Control Workflow Integration", section, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionControlSection_ContainsSuiteModelReferenceSubSection()
    {
        var section = ExtractProductionControlSection(ReadGuide());
        Assert.Contains("### Suite Model Reference", section, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Group 2: DraftFlow evaluation and integration sub-section compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void DraftFlowSection_ReferencesDraftFlowOrg()
    {
        var section    = ExtractProductionControlSection(ReadGuide());
        var draftFlow  = ExtractDraftFlowSection(section);
        Assert.Contains("draftflow.org", draftFlow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DraftFlowSection_MapsToDrawingRevisionRecord()
    {
        var section   = ExtractProductionControlSection(ReadGuide());
        var draftFlow = ExtractDraftFlowSection(section);
        Assert.Contains("DrawingRevisionRecord", draftFlow, StringComparison.Ordinal);
    }

    [Fact]
    public void DraftFlowSection_MapsToIssueSetRecord()
    {
        var section   = ExtractProductionControlSection(ReadGuide());
        var draftFlow = ExtractDraftFlowSection(section);
        Assert.Contains("IssueSetRecord", draftFlow, StringComparison.Ordinal);
    }

    [Fact]
    public void DraftFlowSection_MapsToAuditTrailEntry()
    {
        var section   = ExtractProductionControlSection(ReadGuide());
        var draftFlow = ExtractDraftFlowSection(section);
        Assert.Contains("AuditTrailEntry", draftFlow, StringComparison.Ordinal);
    }

    [Fact]
    public void DraftFlowSection_IncludesFitGapRecordingStep()
    {
        var section   = ExtractProductionControlSection(ReadGuide());
        var draftFlow = ExtractDraftFlowSection(section);
        Assert.Contains("fit-gap", draftFlow, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Group 3: Audit trail implementation sub-section compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void AuditTrailImplSection_DocumentsRequiredActorField()
    {
        var section    = ExtractProductionControlSection(ReadGuide());
        var auditImpl  = ExtractAuditTrailImplSection(section);
        Assert.Contains("Actor", auditImpl, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditTrailImplSection_DocumentsOccurredAtField()
    {
        var section   = ExtractProductionControlSection(ReadGuide());
        var auditImpl = ExtractAuditTrailImplSection(section);
        Assert.Contains("OccurredAt", auditImpl, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditTrailImplSection_DocumentsFromStateAndToStateFields()
    {
        var section   = ExtractProductionControlSection(ReadGuide());
        var auditImpl = ExtractAuditTrailImplSection(section);
        Assert.Contains("FromState", auditImpl, StringComparison.Ordinal);
        Assert.Contains("ToState",   auditImpl, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditTrailImplSection_DocumentsRevisionStateTransitionSteps()
    {
        var section   = ExtractProductionControlSection(ReadGuide());
        var auditImpl = ExtractAuditTrailImplSection(section);
        Assert.Contains("revision state transition", auditImpl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuditTrailImplSection_DocumentsIssueSetStateTransitionSteps()
    {
        var section   = ExtractProductionControlSection(ReadGuide());
        var auditImpl = ExtractAuditTrailImplSection(section);
        Assert.Contains("issue-set state transition", auditImpl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuditTrailImplSection_RequiresPersistBeforeSideEffects()
    {
        var section   = ExtractProductionControlSection(ReadGuide());
        var auditImpl = ExtractAuditTrailImplSection(section);
        Assert.Contains("Persist", auditImpl, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Group 4: Suite model reference table compliance
    // -----------------------------------------------------------------------

    [Fact]
    public void SuiteModelReference_CoversDrawingRevisionRecord()
    {
        var section = ExtractProductionControlSection(ReadGuide());
        Assert.Contains("DrawingRevisionRecord", section, StringComparison.Ordinal);
    }

    [Fact]
    public void SuiteModelReference_CoversDrawingSignoffState()
    {
        var section = ExtractProductionControlSection(ReadGuide());
        Assert.Contains("DrawingSignoffState", section, StringComparison.Ordinal);
    }

    [Fact]
    public void SuiteModelReference_CoversIssueSetRecord()
    {
        var section = ExtractProductionControlSection(ReadGuide());
        Assert.Contains("IssueSetRecord", section, StringComparison.Ordinal);
    }

    [Fact]
    public void SuiteModelReference_CoversIssueSetState()
    {
        var section = ExtractProductionControlSection(ReadGuide());
        Assert.Contains("IssueSetState", section, StringComparison.Ordinal);
    }

    [Fact]
    public void SuiteModelReference_CoversAuditTrailEntry()
    {
        var section = ExtractProductionControlSection(ReadGuide());
        Assert.Contains("AuditTrailEntry", section, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Sanity: verify Suite models have fields that match the documented table
    // -----------------------------------------------------------------------

    [Fact]
    public void AuditTrailEntry_HasAllDocumentedRequiredFields()
    {
        var entry = new AuditTrailEntry
        {
            DrawingId  = "DWG-001",
            RevisionId = "rev-001",
            Action     = "approved",
            Actor      = "J.Smith",
            FromState  = DrawingSignoffState.InReview,
            ToState    = DrawingSignoffState.Approved,
            Notes      = "All comments resolved.",
            OccurredAt = DateTimeOffset.UtcNow,
        };

        // Id is auto-generated by the property initializer (Guid.NewGuid().ToString("N"))
        // and must be non-empty without requiring the caller to assign it.
        Assert.False(string.IsNullOrWhiteSpace(entry.Id));
        Assert.Equal("DWG-001",              entry.DrawingId);
        Assert.Equal("rev-001",              entry.RevisionId);
        Assert.Equal("approved",             entry.Action);
        Assert.Equal("J.Smith",              entry.Actor);
        Assert.Equal(DrawingSignoffState.InReview,  entry.FromState);
        Assert.Equal(DrawingSignoffState.Approved,  entry.ToState);
        Assert.Equal("All comments resolved.", entry.Notes);
        Assert.NotEqual(default, entry.OccurredAt);
    }

    [Fact]
    public void DrawingRevisionRecord_HasAllDocumentedKeyFields()
    {
        var record = new DrawingRevisionRecord
        {
            DrawingId      = "DWG-001",
            RevisionNumber = "B",
            IssuedBy       = "A.Jones",
            State          = DrawingSignoffState.InReview,
            PackageRef     = string.Empty,
        };

        Assert.Equal("DWG-001",  record.DrawingId);
        Assert.Equal("B",        record.RevisionNumber);
        Assert.Equal("A.Jones",  record.IssuedBy);
        Assert.Equal(DrawingSignoffState.InReview, record.State);
        Assert.Equal(string.Empty, record.PackageRef);
    }

    [Fact]
    public void IssueSetRecord_HasAllDocumentedKeyFields()
    {
        var record = new IssueSetRecord
        {
            DrawingSetRef    = "DWG-SET-001",
            IssuedBy         = "J.Smith",
            State            = IssueSetState.InApproval,
            RejectionReason  = string.Empty,
            PackageRef       = string.Empty,
        };
        record.RevisionIds.Add("rev-001");

        Assert.Equal("DWG-SET-001",            record.DrawingSetRef);
        Assert.Equal("J.Smith",                record.IssuedBy);
        Assert.Equal(IssueSetState.InApproval, record.State);
        Assert.Equal(string.Empty,             record.RejectionReason);
        Assert.Equal(string.Empty,             record.PackageRef);
        Assert.NotNull(record.RevisionIds);
        Assert.Single(record.RevisionIds);
    }

    // -----------------------------------------------------------------------
    // Group 5: Reflection-based model compliance
    // Cross-reference the actual C# model definitions against the fields
    // documented in the Suite Model Reference table, so renames or removals
    // of documented properties are caught at test time.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Id")]
    [InlineData("DrawingId")]
    [InlineData("RevisionId")]
    [InlineData("Action")]
    [InlineData("Actor")]
    [InlineData("FromState")]
    [InlineData("ToState")]
    [InlineData("Notes")]
    [InlineData("OccurredAt")]
    public void AuditTrailEntry_DocumentedProperty_ExistsOnModel(string propertyName)
    {
        var prop = typeof(AuditTrailEntry).GetProperty(propertyName);
        Assert.NotNull(prop);
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("DrawingId")]
    [InlineData("RevisionNumber")]
    [InlineData("Description")]
    [InlineData("IssuedBy")]
    [InlineData("State")]
    [InlineData("IssuedAt")]
    [InlineData("PackageRef")]
    public void DrawingRevisionRecord_DocumentedProperty_ExistsOnModel(string propertyName)
    {
        var prop = typeof(DrawingRevisionRecord).GetProperty(propertyName);
        Assert.NotNull(prop);
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("DrawingSetRef")]
    [InlineData("IssuedBy")]
    [InlineData("State")]
    [InlineData("IssuedAt")]
    [InlineData("RevisionIds")]
    [InlineData("RejectionReason")]
    [InlineData("PackageRef")]
    public void IssueSetRecord_DocumentedProperty_ExistsOnModel(string propertyName)
    {
        var prop = typeof(IssueSetRecord).GetProperty(propertyName);
        Assert.NotNull(prop);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("InReview")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    [InlineData("Superseded")]
    public void DrawingSignoffState_DocumentedValue_ExistsInEnum(string valueName)
    {
        Assert.True(Enum.IsDefined(typeof(DrawingSignoffState), valueName),
            $"DrawingSignoffState.{valueName} is documented in AGENT_REPLY_GUIDE.md but does not exist in the enum");
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("InApproval")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    [InlineData("Resubmitted")]
    public void IssueSetState_DocumentedValue_ExistsInEnum(string valueName)
    {
        Assert.True(Enum.IsDefined(typeof(IssueSetState), valueName),
            $"IssueSetState.{valueName} is documented in AGENT_REPLY_GUIDE.md but does not exist in the enum");
    }
}
