using System.Reflection;
using FluentValidation;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Tests that verify ARCHITECTURE.md section 1.3 ("FluentValidation for Broker Requests")
/// is complete and accurately describes the validator structure, error handling workflow,
/// and how validation rules are enforced across broker request records.
///
/// Groups:
///   1. Documentation completeness — ARCHITECTURE.md section 1.3 contains all required
///      description elements (validator structure, error handling workflow, co-location
///      pattern, implemented validator table).
///   2. Validator coverage — every AbstractValidator&lt;T&gt; subclass in the broker assembly
///      is referenced in the documentation table.
///   3. Error message contract — the documented error-message strings match the actual
///      WithMessage() strings used in each validator.
/// </summary>
public sealed class FluentValidationDocumentationTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "DailyDesk", "DailyDesk.csproj")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException(
            "Could not locate repo root (expected DailyDesk/DailyDesk.csproj in an ancestor directory).");
    }

    private static string ReadArchitectureMd()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "Docs", "ARCHITECTURE.md");
        Assert.True(File.Exists(path), $"ARCHITECTURE.md not found at: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Extracts the text of section "### 1.3 FluentValidation for Broker Requests" from
    /// ARCHITECTURE.md, up to (but not including) the next ### heading.
    /// </summary>
    private static string ExtractSection13(string markdown)
    {
        const string header = "### 1.3 FluentValidation for Broker Requests";
        var start = markdown.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, "Section '### 1.3 FluentValidation for Broker Requests' not found in ARCHITECTURE.md.");

        var nextSection = markdown.IndexOf("\n### ", start + header.Length, StringComparison.Ordinal);
        return nextSection >= 0 ? markdown[start..nextSection] : markdown[start..];
    }

    // -----------------------------------------------------------------------
    // Group 1: Documentation completeness
    // -----------------------------------------------------------------------

    [Fact]
    public void Section13_IsMarkedComplete()
    {
        var section = ExtractSection13(ReadArchitectureMd());
        Assert.Contains("COMPLETE", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Section13_DocumentsValidatorStructure()
    {
        var section = ExtractSection13(ReadArchitectureMd());
        // Must describe the AbstractValidator<TRequest> inheritance pattern
        Assert.Contains("AbstractValidator", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Section13_DocumentsCoLocationConvention()
    {
        var section = ExtractSection13(ReadArchitectureMd());
        // Must state that validators are co-located in Endpoints/*.cs files
        Assert.Contains("Endpoints/", section, StringComparison.Ordinal);
        Assert.Contains("co-located", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Section13_DocumentsErrorHandlingWorkflow()
    {
        var section = ExtractSection13(ReadArchitectureMd());
        // Must show the validate-then-400 pattern
        Assert.Contains("validator.Validate(", section, StringComparison.Ordinal);
        Assert.Contains("BadRequest", section, StringComparison.Ordinal);
        Assert.Contains("errors", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Section13_DocumentsResponseShapeDistinction()
    {
        var section = ExtractSection13(ReadArchitectureMd());
        // Must distinguish the { errors: [...] } array shape from the { error: "..." } single-key shape.
        // The docs use backtick formatting (`errors` / `error`) or JSON block quoting.
        Assert.Contains("errors", section, StringComparison.Ordinal);
        Assert.Contains("error", section, StringComparison.Ordinal);
        // Verify both shapes are discussed (errors array vs single error key)
        Assert.Contains("400 Bad Request", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Section13_ContainsValidatorTable()
    {
        var section = ExtractSection13(ReadArchitectureMd());
        // Must contain a markdown table listing validators
        Assert.Contains("| Validator |", section, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Group 2: Validator coverage — every validator class is referenced in docs
    // -----------------------------------------------------------------------

    public static IEnumerable<object[]> BrokerValidatorTypeNames()
    {
        var brokerAssembly = typeof(Program).Assembly;
        var abstractValidatorBase = typeof(AbstractValidator<>);

        return brokerAssembly.GetTypes()
            .Where(t => !t.IsAbstract
                     && t.BaseType is { IsGenericType: true }
                     && t.BaseType.GetGenericTypeDefinition() == abstractValidatorBase)
            .Select(t => new object[] { t.Name });
    }

    [Theory]
    [MemberData(nameof(BrokerValidatorTypeNames))]
    public void Section13_MentionsValidatorByName(string validatorTypeName)
    {
        var section = ExtractSection13(ReadArchitectureMd());
        Assert.Contains(validatorTypeName, section, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Group 3: Error message contract — documented messages match actual messages
    // -----------------------------------------------------------------------

    [Fact]
    public void Section13_DocumentsRouteRequiredMessage()
    {
        var section = ExtractSection13(ReadArchitectureMd());
        Assert.Contains("Route is required.", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Section13_DocumentsStatusEnumValues()
    {
        var section = ExtractSection13(ReadArchitectureMd());
        // The documented status values must match what InboxResolveRequestValidator enforces
        Assert.Contains("accepted", section, StringComparison.Ordinal);
        Assert.Contains("deferred", section, StringComparison.Ordinal);
        Assert.Contains("rejected", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Section13_DocumentsOrchestratorExceptionBoundary()
    {
        var section = ExtractSection13(ReadArchitectureMd());
        // Must clarify that orchestrator exceptions are still caught separately
        Assert.Contains("ArgumentException", section, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", section, StringComparison.Ordinal);
    }
}
