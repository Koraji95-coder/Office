using DailyDesk.Broker;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

public class ChatValidatorTests
{
    private readonly ChatRouteRequestValidator _routeValidator = new();
    private readonly ChatSendRequestValidator _sendValidator = new();

    // ChatRouteRequestValidator — valid routes

    [Theory]
    [InlineData("chief")]
    [InlineData("engineering")]
    [InlineData("suite")]
    [InlineData("business")]
    [InlineData("ml")]
    public void ChatRouteRequestValidator_KnownRoute_IsValid(string route)
    {
        var result = _routeValidator.Validate(new ChatRouteRequest(route));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("CHIEF")]
    [InlineData("Engineering")]
    [InlineData("ML")]
    public void ChatRouteRequestValidator_KnownRouteMixedCase_IsValid(string route)
    {
        var result = _routeValidator.Validate(new ChatRouteRequest(route));
        Assert.True(result.IsValid);
    }

    // ChatRouteRequestValidator — invalid: empty / whitespace

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChatRouteRequestValidator_EmptyOrWhitespaceRoute_FailsWithRequiredMessage(string route)
    {
        var result = _routeValidator.Validate(new ChatRouteRequest(route));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Route is required.");
    }

    // ChatRouteRequestValidator — invalid: unknown route

    [Theory]
    [InlineData("unknown")]
    [InlineData("admin")]
    [InlineData("desk")]
    public void ChatRouteRequestValidator_UnknownRoute_FailsWithKnownRoutesMessage(string route)
    {
        var result = _routeValidator.Validate(new ChatRouteRequest(route));
        Assert.False(result.IsValid);
        var expected = $"Route must be one of: {string.Join(", ", OfficeRouteCatalog.KnownRoutes)}.";
        Assert.Contains(result.Errors, e => e.ErrorMessage == expected);
    }

    // ChatSendRequestValidator — valid prompt

    [Fact]
    public void ChatSendRequestValidator_NonEmptyPrompt_IsValid()
    {
        var result = _sendValidator.Validate(new ChatSendRequest("Hello!", null));
        Assert.True(result.IsValid);
    }

    // ChatSendRequestValidator — invalid: empty / whitespace prompt

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChatSendRequestValidator_EmptyOrWhitespacePrompt_FailsWithRequiredMessage(string prompt)
    {
        var result = _sendValidator.Validate(new ChatSendRequest(prompt, null));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Prompt is required.");
    }
}

public class StudyValidatorTests
{
    private readonly StudyScoreDefenseRequestValidator _defenseValidator = new();
    private readonly StudySaveReflectionRequestValidator _reflectionValidator = new();

    // StudyScoreDefenseRequestValidator — valid answer

    [Fact]
    public void StudyScoreDefenseRequestValidator_NonEmptyAnswer_IsValid()
    {
        var result = _defenseValidator.Validate(new StudyScoreDefenseRequest("My answer"));
        Assert.True(result.IsValid);
    }

    // StudyScoreDefenseRequestValidator — invalid: empty / whitespace answer

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StudyScoreDefenseRequestValidator_EmptyOrWhitespaceAnswer_FailsWithRequiredMessage(string answer)
    {
        var result = _defenseValidator.Validate(new StudyScoreDefenseRequest(answer));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Answer is required.");
    }

    // StudySaveReflectionRequestValidator — valid reflection

    [Fact]
    public void StudySaveReflectionRequestValidator_NonEmptyReflection_IsValid()
    {
        var result = _reflectionValidator.Validate(new StudySaveReflectionRequest("My reflection"));
        Assert.True(result.IsValid);
    }

    // StudySaveReflectionRequestValidator — invalid: empty / whitespace reflection

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StudySaveReflectionRequestValidator_EmptyOrWhitespaceReflection_FailsWithRequiredMessage(string reflection)
    {
        var result = _reflectionValidator.Validate(new StudySaveReflectionRequest(reflection));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Reflection is required.");
    }
}
