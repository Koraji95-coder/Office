using System.Text.Json;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace DailyDesk.Services;

public sealed class OllamaService : IModelProvider
{
    public const string OllamaProviderId = "ollama";
    public const string OllamaProviderLabel = "Ollama (local)";

    private readonly OllamaApiClient _client;
    private readonly ProcessRunner _processRunner;
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public string ProviderId => OllamaProviderId;
    public string ProviderLabel => OllamaProviderLabel;

    public OllamaService(string endpoint, ProcessRunner processRunner)
    {
        var uri = new Uri(endpoint.EndsWith("/") ? endpoint : $"{endpoint}/");
        var httpClient = new System.Net.Http.HttpClient
        {
            BaseAddress = uri,
            Timeout = TimeSpan.FromSeconds(90),
        };
        _client = new OllamaApiClient(httpClient);
        _processRunner = processRunner;
    }

    public async Task<IReadOnlyList<string>> GetInstalledModelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var modelsResponse = await _client.ListLocalModelsAsync(cancellationToken);
            var models = modelsResponse
                .Select(m => m.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (models.Count > 0)
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
        var request = new ChatRequest
        {
            Model = model,
            Stream = false,
            Messages =
            [
                new Message(ChatRole.System, systemPrompt),
                new Message(ChatRole.User, userPrompt),
            ],
        };

        var responseStream = _client.ChatAsync(request, cancellationToken);
        ChatResponseStream? lastChunk = null;

        await foreach (var chunk in responseStream.WithCancellation(cancellationToken))
        {
            lastChunk = chunk;
        }

        return lastChunk?.Message?.Content?.Trim() ?? string.Empty;
    }

    public async Task<T?> GenerateJsonAsync<T>(
        string model,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default
    )
    {
        var request = new ChatRequest
        {
            Model = model,
            Stream = false,
            Format = "json",
            Messages =
            [
                new Message(ChatRole.System, systemPrompt),
                new Message(ChatRole.User, userPrompt),
            ],
        };

        var responseStream = _client.ChatAsync(request, cancellationToken);
        ChatResponseStream? lastChunk = null;

        await foreach (var chunk in responseStream.WithCancellation(cancellationToken))
        {
            lastChunk = chunk;
        }

        var json = lastChunk?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }
}
