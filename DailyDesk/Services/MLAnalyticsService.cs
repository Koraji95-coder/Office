using System.IO;
using System.Text;
using System.Text.Json;
using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class MLAnalyticsService
{
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromSeconds(60);

    private readonly ProcessRunner _processRunner;
    private readonly string _scriptsDirectory;
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public MLAnalyticsService(ProcessRunner processRunner, string scriptsDirectory)
    {
        _processRunner = processRunner;
        _scriptsDirectory = scriptsDirectory;
    }

    public async Task<MLAnalyticsResult> RunLearningAnalyticsAsync(
        IReadOnlyList<TrainingAttemptRecord> attempts,
        IReadOnlyList<OperatorActivityRecord> decisions,
        CancellationToken cancellationToken = default
    )
    {
        var input = new
        {
            trainingAttempts = attempts.Select(a => new
            {
                completedAt = a.CompletedAt.ToString("O"),
                questions = a.Questions.Select(q => new
                {
                    topic = q.Topic,
                    correct = q.Correct,
                }).ToArray(),
            }).ToArray(),
            operatorDecisions = decisions.Select(d => new
            {
                status = d.EventType,
                timestamp = d.OccurredAt.ToString("O"),
            }).ToArray(),
        };

        try
        {
            var result = await RunPythonScriptAsync<MLAnalyticsResult>(
                "ml_learning_analytics.py",
                input,
                cancellationToken
            );

            return result ?? BuildFallbackAnalytics(attempts);
        }
        catch
        {
            return BuildFallbackAnalytics(attempts);
        }
    }

    public async Task<MLEmbeddingsResult> RunDocumentEmbeddingsAsync(
        IReadOnlyList<LearningDocument> documents,
        string? query = null,
        CancellationToken cancellationToken = default
    )
    {
        var input = new
        {
            documents = documents.Select(d => new
            {
                id = d.FullPath,
                title = d.FileName,
                text = d.ExtractedText?.Length > 5000
                    ? d.ExtractedText[..5000]
                    : d.ExtractedText ?? string.Empty,
            }).ToArray(),
            query,
        };

        try
        {
            var result = await RunPythonScriptAsync<MLEmbeddingsResult>(
                "ml_document_embeddings.py",
                input,
                cancellationToken
            );

            return result ?? BuildFallbackEmbeddings();
        }
        catch
        {
            return BuildFallbackEmbeddings();
        }
    }

    public async Task<MLForecastResult> RunProgressForecastAsync(
        IReadOnlyList<TrainingAttemptRecord> attempts,
        CancellationToken cancellationToken = default
    )
    {
        var input = new
        {
            trainingAttempts = attempts.Select(a => new
            {
                completedAt = a.CompletedAt.ToString("O"),
                questions = a.Questions.Select(q => new
                {
                    topic = q.Topic,
                    correct = q.Correct,
                }).ToArray(),
            }).ToArray(),
        };

        try
        {
            var result = await RunPythonScriptAsync<MLForecastResult>(
                "ml_progress_forecast.py",
                input,
                cancellationToken
            );

            return result ?? BuildFallbackForecast();
        }
        catch
        {
            return BuildFallbackForecast();
        }
    }

    public async Task<SuiteMLArtifactBundle> GenerateSuiteArtifactsAsync(
        MLAnalyticsResult analytics,
        MLEmbeddingsResult embeddings,
        MLForecastResult forecast,
        CancellationToken cancellationToken = default
    )
    {
        var input = new
        {
            analytics,
            embeddings,
            forecast,
        };

        try
        {
            var result = await RunPythonScriptAsync<SuiteMLArtifactBundle>(
                "ml_suite_artifacts.py",
                input,
                cancellationToken
            );

            return result ?? BuildFallbackArtifacts();
        }
        catch
        {
            return BuildFallbackArtifacts();
        }
    }

    public async Task<string> ExportArtifactsAsync(
        SuiteMLArtifactBundle bundle,
        string stateRootPath,
        CancellationToken cancellationToken = default
    )
    {
        var artifactsDirectory = Path.Combine(stateRootPath, "ml-artifacts");
        Directory.CreateDirectory(artifactsDirectory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var filePath = Path.Combine(artifactsDirectory, $"suite-artifacts-{timestamp}.json");

        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        return filePath;
    }

    private async Task<T?> RunPythonScriptAsync<T>(
        string scriptName,
        object input,
        CancellationToken cancellationToken
    )
    {
        var scriptPath = Path.Combine(_scriptsDirectory, scriptName);
        if (!File.Exists(scriptPath))
        {
            return default;
        }

        var inputJson = JsonSerializer.Serialize(input, _jsonOptions);
        var tempInputPath = Path.Combine(Path.GetTempPath(), $"office-ml-{Guid.NewGuid()}.json");

        try
        {
            await File.WriteAllTextAsync(tempInputPath, inputJson, cancellationToken);

            var output = await _processRunner.RunAsync(
                "python",
                $"\"{scriptPath}\" < \"{tempInputPath}\"",
                _scriptsDirectory,
                cancellationToken
            );

            if (string.IsNullOrWhiteSpace(output))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(output, _jsonOptions);
        }
        finally
        {
            try
            {
                if (File.Exists(tempInputPath))
                {
                    File.Delete(tempInputPath);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private static MLAnalyticsResult BuildFallbackAnalytics(
        IReadOnlyList<TrainingAttemptRecord> attempts
    )
    {
        var topicAccuracy = new Dictionary<string, (int correct, int total)>();
        foreach (var attempt in attempts)
        {
            foreach (var question in attempt.Questions)
            {
                if (!topicAccuracy.ContainsKey(question.Topic))
                {
                    topicAccuracy[question.Topic] = (0, 0);
                }

                var (correct, total) = topicAccuracy[question.Topic];
                topicAccuracy[question.Topic] = (
                    correct + (question.Correct ? 1 : 0),
                    total + 1
                );
            }
        }

        var weak = new List<MLTopicEntry>();
        var strong = new List<MLTopicEntry>();
        foreach (var (topic, (correct, total)) in topicAccuracy)
        {
            var accuracy = total > 0 ? (double)correct / total : 0.0;
            var entry = new MLTopicEntry
            {
                Topic = topic,
                Accuracy = Math.Round(accuracy, 3),
                TotalQuestions = total,
                CorrectCount = correct,
            };

            if (accuracy < 0.6)
            {
                weak.Add(entry);
            }
            else
            {
                strong.Add(entry);
            }
        }

        var overallReadiness = topicAccuracy.Count > 0
            ? topicAccuracy.Values.Average(v => (double)v.correct / v.total)
            : 0.0;

        return new MLAnalyticsResult
        {
            Ok = true,
            Engine = "fallback",
            WeakTopics = weak.OrderBy(t => t.Accuracy).ToList(),
            StrongTopics = strong.OrderByDescending(t => t.Accuracy).ToList(),
            OverallReadiness = Math.Round(overallReadiness, 3),
            OperatorPattern = new MLOperatorPattern(),
            AdaptiveSchedule = weak
                .OrderBy(t => t.Accuracy)
                .Take(5)
                .Select((t, i) => new MLScheduleItem
                {
                    Topic = t.Topic,
                    Priority = i + 1,
                    RecommendedSessionType = t.Accuracy < 0.4 ? "practice" : "defense",
                    IntervalDays = Math.Max(1, (int)((1.0 - t.Accuracy) * 7)),
                    Reason = $"Accuracy {t.Accuracy:P0} is below threshold",
                })
                .ToList(),
            ReadinessBreakdown = topicAccuracy
                .Select(kv => new MLReadinessEntry
                {
                    Topic = kv.Key,
                    Readiness = Math.Round((double)kv.Value.correct / kv.Value.total, 3),
                    Confidence = Math.Min(1.0, kv.Value.total / 20.0),
                })
                .ToList(),
        };
    }

    private static MLEmbeddingsResult BuildFallbackEmbeddings() =>
        new()
        {
            Ok = true,
            Engine = "fallback",
        };

    private static MLForecastResult BuildFallbackForecast() =>
        new()
        {
            Ok = true,
            Engine = "fallback",
        };

    private static SuiteMLArtifactBundle BuildFallbackArtifacts() =>
        new()
        {
            Ok = true,
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
        };
}
