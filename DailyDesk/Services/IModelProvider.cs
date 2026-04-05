namespace DailyDesk.Services;

public interface IModelProvider
{
    string ProviderId { get; }
    string ProviderLabel { get; }

    Task<IReadOnlyList<string>> GetInstalledModelsAsync(
        CancellationToken cancellationToken = default
    );

    Task<string> GenerateAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default
    );

    Task<T?> GenerateJsonAsync<T>(
        string model,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks whether the model provider backend is reachable.
    /// Returns true if the provider responds to a lightweight ping.
    /// </summary>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}
