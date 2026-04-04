using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DailyDesk.Services;

public sealed class OllamaService : IModelProvider
{
    public const string OllamaProviderId = "ollama";
    public const string OllamaProviderLabel = "Ollama (local)";

    private readonly HttpClient _httpClient;
    private readonly ProcessRunner _processRunner;
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public string ProviderId => OllamaProviderId;
    public string ProviderLabel => OllamaProviderLabel;

    public OllamaService(string endpoint, ProcessRunner processRunner)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(endpoint.EndsWith("/") ? endpoint : $"{endpoint}/"),
            Timeout = TimeSpan.FromSeconds(90),
        };
        _processRunner = processRunner;
    }

    public async Task<IReadOnlyList<string>> GetInstalledModelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/tags", cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var tags = JsonSerializer.Deserialize<OllamaTagsResponse>(payload, _jsonOptions);
            var models = tags?.Models?
                .Select(model => model.Model ?? model.Name)
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Select(model => model!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (models is { Count: > 0 })
            {
                return models;
            }
        }
        catch
        {
            // Fall back to CLI.
        }

        try
        {
            var output = await _processRunner.RunAsync("ollama", "list", null, cancellationToken);
            return output
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task<string> GenerateAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default
    )
    {
        var request = new OllamaChatRequest(
            model,
            new[]
            {
                new OllamaMessage("system", systemPrompt),
                new OllamaMessage("user", userPrompt),
            },
            Stream: false
        );

        using var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json"
        );
        using var response = await _httpClient.PostAsync("api/chat", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var chat = JsonSerializer.Deserialize<OllamaChatResponse>(payload, _jsonOptions);
        return chat?.Message?.Content?.Trim() ?? string.Empty;
    }

    public async Task<T?> GenerateJsonAsync<T>(
        string model,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default
    )
    {
        var request = new OllamaChatRequest(
            model,
            new[]
            {
                new OllamaMessage("system", systemPrompt),
                new OllamaMessage("user", userPrompt),
            },
            Stream: false,
            Format: "json"
        );

        using var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json"
        );
        using var response = await _httpClient.PostAsync("api/chat", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var chat = JsonSerializer.Deserialize<OllamaChatResponse>(payload, _jsonOptions);
        var json = chat?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    private sealed record OllamaTagsResponse(IReadOnlyList<OllamaModelTag>? Models);
    private sealed record OllamaModelTag(string? Name, string? Model);
    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaMessage> Messages,
        bool Stream,
        string? Format = null
    );
    private sealed record OllamaMessage(string Role, string Content);
    private sealed record OllamaChatResponse(OllamaMessage? Message);
}
