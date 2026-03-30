namespace DailyDesk.Models;

public sealed class AgentPolicy
{
    public string Role { get; set; } = string.Empty;
    public string AutonomyLevel { get; set; } = "Prepare";
    public List<string> AllowedActionClasses { get; set; } = [];
    public bool RequiresApproval { get; set; } = true;
    public string ReviewCadence { get; set; } = "Daily";

    public string AllowedActionsSummary =>
        AllowedActionClasses.Count == 0 ? "none configured" : string.Join(", ", AllowedActionClasses);

    public string DisplaySummary =>
        $"{Role} | {AutonomyLevel} | {(RequiresApproval ? "approval gate" : "self-serve")} | {ReviewCadence}";
}
