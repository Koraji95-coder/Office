using System.Text.Json.Serialization;

namespace DailyDesk.Models;

public sealed class DeskMessageRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeskId { get; set; } = string.Empty;
    public string Role { get; set; } = "assistant";
    public string Author { get; set; } = string.Empty;
    public string Kind { get; set; } = "chat";
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    [JsonIgnore]
    public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsAssistant => !IsUser;

    [JsonIgnore]
    public string Meta =>
        $"{Author} | {CreatedAt:MMM d, h:mm tt}";
}
