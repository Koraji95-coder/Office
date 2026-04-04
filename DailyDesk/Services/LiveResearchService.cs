using System.Net;
using System.Net.Http;
using AngleSharp;
using AngleSharp.Html.Parser;
using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class LiveResearchService
{
    private static readonly HtmlParser HtmlParser = new();

    private readonly HttpClient _httpClient;
    private readonly IModelProvider _modelProvider;

    public LiveResearchService(IModelProvider modelProvider)
    {
        _modelProvider = modelProvider;

        var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(25),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "DailyDesk/1.0 (Windows research workstation)"
        );
    }

    public async Task<ResearchReport> RunAsync(
        string query,
        string perspective,
        string model,
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        CancellationToken cancellationToken = default
    )
    {
        var trimmedQuery = string.IsNullOrWhiteSpace(query)
            ? "electrical drawing QA automation review gates"
            : query.Trim();

        var searchResults = await SearchAsync(trimmedQuery, cancellationToken);
        var sources = await EnrichSourcesAsync(searchResults.Take(4).ToList(), cancellationToken);

        if (sources.Count == 0)
        {
            return BuildEmptyReport(trimmedQuery, perspective, model);
        }

        try
        {
            var generated = await _modelProvider.GenerateJsonAsync<ResearchSynthesisContract>(
                model,
                BuildSystemPrompt(perspective),
                BuildUserPrompt(
                    trimmedQuery,
                    perspective,
                    sources,
                    snapshot,
                    historySummary,
                    learningProfile,
                    learningLibrary
                ),
                cancellationToken
            );

            var converted = ConvertReport(
                trimmedQuery,
                perspective,
                model,
                _modelProvider.ProviderId,
                sources,
                generated
            );
            if (converted is not null)
            {
                return converted;
            }
        }
        catch
        {
            // Fall back to deterministic synthesis if the summarizer fails.
        }

        return BuildFallbackReport(trimmedQuery, perspective, model, sources, snapshot, historySummary);
    }

    private async Task<IReadOnlyList<ResearchSource>> SearchAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        var endpoint =
            $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = HtmlParser.ParseDocument(html);

        var linkElements = document.QuerySelectorAll("a.result__a[href]");
        var snippetElements = document.QuerySelectorAll("a.result__snippet");
        var results = new List<ResearchSource>();

        for (var index = 0; index < linkElements.Length && results.Count < 8; index++)
        {
            var element = linkElements[index];
            var href = element.GetAttribute("href") ?? string.Empty;
            var url = NormalizeSearchUrl(href);
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var title = element.TextContent.Trim();
            var snippet = index < snippetElements.Length
                ? snippetElements[index].TextContent.Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            results.Add(
                new ResearchSource
                {
                    Title = title,
                    Url = url,
                    Domain = uri.Host,
                    SearchSnippet = snippet,
                }
            );
        }

        return results
            .GroupBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private async Task<IReadOnlyList<ResearchSource>> EnrichSourcesAsync(
        IReadOnlyList<ResearchSource> sources,
        CancellationToken cancellationToken
    )
    {
        var tasks = sources.Select(source => EnrichSourceAsync(source, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results
            .Where(source => !string.IsNullOrWhiteSpace(source.Url))
            .ToList();
    }

    private async Task<ResearchSource> EnrichSourceAsync(
        ResearchSource source,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var response = await _httpClient.GetAsync(source.Url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !mediaType.Contains("text", StringComparison.OrdinalIgnoreCase))
            {
                return source;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var extract = ExtractPreview(html);

            return new ResearchSource
            {
                Title = source.Title,
                Url = source.Url,
                Domain = source.Domain,
                SearchSnippet = source.SearchSnippet,
                Extract = extract,
            };
        }
        catch
        {
            return source;
        }
    }

    private static string BuildSystemPrompt(string perspective) =>
        $"""
        You are the {perspective} agent inside Daily Desk.
        Synthesize live web research for a user who is growing as an electrical engineer, improving Suite, and evaluating future monetization paths.
        Use only the supplied sources. Stay conservative and practical.
        Return strict JSON only with:
        - summary
        - keyTakeaways (3 to 5 items)
        - actionMoves (3 to 5 items)
        """;

    private static string BuildUserPrompt(
        string query,
        string perspective,
        IReadOnlyList<ResearchSource> sources,
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary
    )
    {
        var sourcePack = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            sources.Select(
                (source, index) =>
                    $"""
                    Source {index + 1}
                    - title: {source.Title}
                    - domain: {source.Domain}
                    - url: {source.Url}
                    - search snippet: {source.SearchSnippet}
                    - extracted page text: {source.Extract}
                    """
            )
        );

        return
            $"""
            Research query: {query}
            Agent perspective: {perspective}

            Current Suite context:
            - hot areas: {JoinOrNone(snapshot.HotAreas)}
            - next session tasks: {JoinOrNone(snapshot.NextSessionTasks.Take(4).ToList())}
            - monetization leads: {JoinOrNone(snapshot.MonetizationMoves.Take(4).ToList())}

            Current training memory:
            - practice summary: {historySummary.OverallSummary}
            - defense summary: {historySummary.DefenseSummary}
            - review queue: {historySummary.ReviewQueueSummary}
            - recent reflections: {JoinOrNone(historySummary.RecentReflections.Take(3).Select(item => $"{item.Mode} {item.Focus}: {item.Reflection}").ToList())}

            Learning profile:
            - summary: {learningProfile.Summary}
            - current need: {learningProfile.CurrentNeed}
            - active topics: {JoinOrNone(learningProfile.ActiveTopics.Take(6).ToList())}
            - imported knowledge: {JoinOrNone(learningLibrary.Documents.Take(4).Select(document => document.PromptSummary).ToList())}

            Source pack:
            {sourcePack}
            """;
    }

    private static ResearchReport? ConvertReport(
        string query,
        string perspective,
        string model,
        string providerId,
        IReadOnlyList<ResearchSource> sources,
        ResearchSynthesisContract? contract
    )
    {
        if (contract is null || string.IsNullOrWhiteSpace(contract.Summary))
        {
            return null;
        }

        var takeaways = contract.KeyTakeaways?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Take(5)
            .ToList();
        var actions = contract.ActionMoves?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Take(5)
            .ToList();

        return new ResearchReport
        {
            Query = query,
            Perspective = perspective,
            Model = model,
            Summary = contract.Summary.Trim(),
            GenerationSource = $"live web + {providerId} synthesis",
            KeyTakeaways = takeaways ?? [],
            ActionMoves = actions ?? [],
            Sources = sources,
        };
    }

    private static ResearchReport BuildFallbackReport(
        string query,
        string perspective,
        string model,
        IReadOnlyList<ResearchSource> sources,
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary
    )
    {
        var bestSource = sources.FirstOrDefault();
        var takeaways = sources
            .Take(4)
            .Select(
                source =>
                {
                    var text = !string.IsNullOrWhiteSpace(source.Extract)
                        ? source.Extract
                        : source.SearchSnippet;
                    return $"{source.Domain}: {text}";
                }
            )
            .ToList();

        return new ResearchReport
        {
            Query = query,
            Perspective = perspective,
            Model = model,
            Summary =
                $"Live research collected {sources.Count} sources for '{query}'. Start with {bestSource?.Title ?? "the top source"}, then compare the findings against {snapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot"} and your current review queue ({historySummary.ReviewQueueSummary}).",
            GenerationSource = "live web + fallback synthesis",
            KeyTakeaways = takeaways,
            ActionMoves =
            [
                $"Turn one source insight into a concrete Daily Desk or Suite test related to {snapshot.HotAreas.FirstOrDefault() ?? "current repo work"}.",
                "Compare source claims against your own notes before treating them as a rule.",
                "If a source looks commercially relevant, capture how it changes career proof or monetization framing.",
            ],
            Sources = sources,
        };
    }

    private static ResearchReport BuildEmptyReport(string query, string perspective, string model) =>
        new()
        {
            Query = query,
            Perspective = perspective,
            Model = model,
            Summary =
                "No live sources were collected for this query. Refine the wording and try again with a narrower topic.",
            GenerationSource = "live web search returned no usable sources",
            KeyTakeaways =
            [
                "Prefer a tighter query with 3 to 8 specific terms.",
                "Lead with the exact engineering, repo, or business concept you want to test.",
            ],
            ActionMoves =
            [
                "Try a narrower query.",
                "Add the target standard, technology, or workflow name to the search.",
            ],
        };

    private static string NormalizeSearchUrl(string rawHref)
    {
        if (string.IsNullOrWhiteSpace(rawHref))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(rawHref.Trim());
        if (decoded.StartsWith("//", StringComparison.Ordinal))
        {
            decoded = $"https:{decoded}";
        }

        var markerIndex = decoded.IndexOf("uddg=", StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            var encodedStart = markerIndex + "uddg=".Length;
            var encodedUrl = encodedStart >= decoded.Length ? string.Empty : decoded[encodedStart..];
            var ampIndex = encodedUrl.IndexOf('&');
            if (ampIndex >= 0)
            {
                encodedUrl = encodedUrl[..ampIndex];
            }

            decoded = WebUtility.UrlDecode(encodedUrl);
        }

        return decoded;
    }

    private static string ExtractPreview(string html)
    {
        var document = HtmlParser.ParseDocument(html);

        var metaDescription = TryExtractMetaDescription(document);
        if (!string.IsNullOrWhiteSpace(metaDescription))
        {
            return metaDescription;
        }

        // Remove script/style/noscript/svg/iframe elements before extracting text.
        foreach (var element in document.QuerySelectorAll("script, style, noscript, svg, iframe").ToList())
        {
            element.Remove();
        }

        var text = NormalizeWhitespace(document.Body?.TextContent ?? string.Empty);

        if (text.Length > 900)
        {
            return $"{text[..897]}...";
        }

        return text;
    }

    private static string TryExtractMetaDescription(AngleSharp.Dom.IDocument document)
    {
        var meta = document.QuerySelector("meta[name='description']")
                   ?? document.QuerySelector("meta[property='og:description']");
        var content = meta?.GetAttribute("content");
        return NormalizeWhitespace(content ?? string.Empty);
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(value, "\\s+", " ").Trim();
    }

    private static string JoinOrNone(IReadOnlyList<string> items) =>
        items.Count == 0 ? "none recorded" : string.Join("; ", items);

    private sealed class ResearchSynthesisContract
    {
        public string? Summary { get; set; }
        public List<string>? KeyTakeaways { get; set; }
        public List<string>? ActionMoves { get; set; }
    }
}
