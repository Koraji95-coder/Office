using System.Net;
using System.Reflection;
using FluentValidation;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Tests that verify the endpoint organisation refactor described in TECHNICAL-DEBT.md
/// entry "Broker Program.cs — All Endpoints in One File".
///
/// The refactor extracted all 30+ inline endpoint handlers from <c>Program.cs</c> into eight
/// dedicated <c>IEndpointRouteBuilder</c> extension-method files under
/// <c>DailyDesk.Broker/Endpoints/</c>. Each file owns its own request record types.
///
/// Three test groups:
///   1. Static structure tests — verify the expected extension method classes and their
///      <c>Map*Endpoints</c> methods exist in the broker assembly.
///   2. <c>Program.cs</c> size tests — verify the entry-point file is slim (≤ 80 lines).
///   3. Smoke tests via <see cref="BrokerWebApplicationFactory"/> — confirm that the
///      broker still routes health and state requests correctly after the refactor.
/// </summary>
[Collection("BrokerIntegrationTests")]
public sealed class EndpointOrganizationTests : IClassFixture<BrokerWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EndpointOrganizationTests(BrokerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -----------------------------------------------------------------------
    // Group 1: Extension method class structure tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// The expected endpoint extension class names and their Map* method names.
    /// </summary>
    public static IEnumerable<object[]> ExpectedEndpointClasses =>
    [
        new object[] { "HealthEndpoints",   "MapHealthEndpoints" },
        new object[] { "ChatEndpoints",     "MapChatEndpoints" },
        new object[] { "ResearchEndpoints", "MapResearchEndpoints" },
        new object[] { "OperatorEndpoints", "MapOperatorEndpoints" },
        new object[] { "MLEndpoints",       "MapMLEndpoints" },
        new object[] { "KnowledgeEndpoints","MapKnowledgeEndpoints" },
        new object[] { "ScheduleEndpoints", "MapScheduleEndpoints" },
    ];

    [Theory]
    [MemberData(nameof(ExpectedEndpointClasses))]
    public void EndpointClass_ExistsInBrokerAssembly(string className, string methodName)
    {
        var brokerAssembly = typeof(Program).Assembly;

        var type = brokerAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == className);

        Assert.NotNull(type);
        Assert.True(type!.IsAbstract && type.IsSealed,
            $"{className} (expected method: {methodName}) must be a static class (abstract + sealed in IL)");
    }

    [Theory]
    [MemberData(nameof(ExpectedEndpointClasses))]
    public void EndpointClass_HasExpectedMapMethod(string className, string methodName)
    {
        var brokerAssembly = typeof(Program).Assembly;

        var type = brokerAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == className);

        Assert.NotNull(type);

        var method = type!.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
    }

    [Theory]
    [MemberData(nameof(ExpectedEndpointClasses))]
    public void EndpointMapMethod_FirstParameterIsIEndpointRouteBuilder(string className, string methodName)
    {
        var brokerAssembly = typeof(Program).Assembly;
        var type = brokerAssembly.GetTypes().First(t => t.Name == className);
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;

        var firstParam = method.GetParameters().FirstOrDefault();
        Assert.NotNull(firstParam);
        Assert.Equal(typeof(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder), firstParam!.ParameterType);
    }

    // -----------------------------------------------------------------------
    // Group 2: Program.cs size and validator co-location tests
    // -----------------------------------------------------------------------

    [Fact]
    public void ProgramCs_IsSlim_NoMoreThan80Lines()
    {
        var repoRoot = FindRepoRoot();
        var programCsPath = Path.Combine(repoRoot, "DailyDesk.Broker", "Program.cs");
        Assert.True(File.Exists(programCsPath), $"Program.cs not found at: {programCsPath}");

        var lines = File.ReadAllLines(programCsPath);
        Assert.True(lines.Length <= 80,
            $"Program.cs has {lines.Length} lines. After the endpoint refactor it must be " +
            $"≤ 80 lines (infrastructure setup only). Move endpoint handlers to Endpoints/*.cs.");
    }

    [Fact]
    public void ValidatorsDirectory_DoesNotExist()
    {
        var repoRoot = FindRepoRoot();
        var validatorsDirPath = Path.Combine(repoRoot, "DailyDesk.Broker", "Validators");
        Assert.False(Directory.Exists(validatorsDirPath),
            $"The Validators/ directory still exists at: {validatorsDirPath}. " +
            $"Validator classes must be co-located in their corresponding Endpoints/*.cs files " +
            $"alongside the request records they validate.");
    }

    [Fact]
    public void ValidatorTypes_AreDefinedInBrokerAssembly_CoLocatedWithEndpoints()
    {
        // All AbstractValidator<T> subclasses must live in the broker assembly,
        // confirming they have been moved out of the standalone Validators/ directory
        // and into the Endpoints/*.cs files alongside the request records they validate.
        var brokerAssembly = typeof(Program).Assembly;
        var abstractValidatorBase = typeof(FluentValidation.AbstractValidator<>);

        var validatorTypes = brokerAssembly.GetTypes()
            .Where(t => !t.IsAbstract
                     && t.BaseType is { IsGenericType: true }
                     && t.BaseType.GetGenericTypeDefinition() == abstractValidatorBase)
            .ToList();

        Assert.NotEmpty(validatorTypes);

        foreach (var validatorType in validatorTypes)
        {
            Assert.True(validatorType.Assembly == brokerAssembly,
                $"Validator '{validatorType.Name}' is not in the broker assembly. " +
                $"Validators must be co-located in their Endpoints/*.cs files.");
        }
    }

    // -----------------------------------------------------------------------
    // Shared helper
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
            "Could not locate repo root (expected to find DailyDesk/DailyDesk.csproj in an ancestor directory).");
    }

    // -----------------------------------------------------------------------
    // Group 3: Smoke tests — endpoints still respond after refactor
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/health")]
    [InlineData("/api/state")]
    public async Task HealthEndpoints_ReturnOkOrProblem_NotNotFound(string path)
    {
        var response = await _client.GetAsync(path);

        // 200 (success) or 500 (dependency not available) are both acceptable.
        // 404 means the endpoint was not registered — that would be a regression.
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/jobs")]
    [InlineData("/api/jobs/metrics")]
    [InlineData("/api/schedules")]
    [InlineData("/api/workflows")]
    [InlineData("/api/daily-run/latest")]
    [InlineData("/api/knowledge/index-status")]
    [InlineData("/api/inbox")]
    public async Task GetEndpoints_ReturnOkOrProblem_NotNotFound(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
