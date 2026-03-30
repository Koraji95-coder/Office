using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class OralDefenseService
{
    private readonly OllamaService _ollamaService;
    private readonly string _model;

    public OralDefenseService(OllamaService ollamaService, string model)
    {
        _ollamaService = ollamaService;
        _model = model;
    }

    public async Task<OralDefenseScenario> CreateScenarioAsync(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        IReadOnlyList<StudyTrack> studyTracks,
        string? preferredTopic = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var generated = await _ollamaService.GenerateJsonAsync<OralDefenseContract>(
                _model,
                BuildSystemPrompt(),
                BuildUserPrompt(
                    snapshot,
                    historySummary,
                    learningProfile,
                    learningLibrary,
                    studyTracks,
                    preferredTopic
                ),
                cancellationToken
            );

            var converted = ConvertContract(
                generated,
                preferredTopic,
                historySummary,
                learningProfile
            );
            if (converted is not null)
            {
                return converted;
            }
        }
        catch
        {
            // Fall back to deterministic heuristics when generation fails.
        }

        return BuildFallbackScenario(
            snapshot,
            historySummary,
            learningProfile,
            learningLibrary,
            preferredTopic
        );
    }

    public async Task<DefenseEvaluation> ScoreResponseAsync(
        OralDefenseScenario scenario,
        string answer,
        SuiteSnapshot snapshot,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var generated = await _ollamaService.GenerateJsonAsync<DefenseEvaluationContract>(
                _model,
                BuildScoringSystemPrompt(),
                BuildScoringUserPrompt(scenario, answer, snapshot, learningProfile, learningLibrary),
                cancellationToken
            );

            var converted = ConvertEvaluation(generated);
            if (converted is not null)
            {
                return converted;
            }
        }
        catch
        {
            // Fall back to heuristic scoring when the mentor model is unavailable.
        }

        return BuildFallbackEvaluation(scenario, answer);
    }

    private static string BuildSystemPrompt() =>
        """
        You create oral defense drills for an aspiring electrical engineer who is building operator-first automation software.
        Return strict JSON only.
        Produce:
        - topic
        - title
        - prompt
        - whatGoodLooksLike
        - suiteConnection
        - followUpQuestions (3 to 5 items)
        Keep the drill grounded in engineering judgment, standards, safety, production reliability, and clear technical communication.
        """;

    private static string BuildUserPrompt(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        IReadOnlyList<StudyTrack> studyTracks,
        string? preferredTopic
    )
    {
        var knowledgeContext = KnowledgePromptContextBuilder.BuildRelevantContext(
            learningLibrary,
            new[]
            {
                preferredTopic,
                learningProfile.CurrentNeed,
                historySummary.DefenseSummary,
                string.Join("; ", historySummary.ReviewRecommendations.Take(4).Select(item => item.Topic)),
                string.Join("; ", historySummary.WeakTopics.Take(4).Select(item => item.Topic)),
            },
            maxDocuments: 3,
            maxTotalCharacters: 2400,
            maxExcerptCharacters: 720
        );

        return $"""
        Build one oral defense drill.
        Preferred target topic: {preferredTopic ?? "none specified"}

        Current Suite context:
        - hot areas: {JoinOrNone(snapshot.HotAreas)}
        - next session tasks: {JoinOrNone(snapshot.NextSessionTasks.Take(4).ToList())}
        - monetization leads: {JoinOrNone(snapshot.MonetizationMoves.Take(4).ToList())}

        Current review queue:
        - review recommendations: {JoinOrNone(historySummary.ReviewRecommendations.Select(item => $"{item.Topic} ({item.Priority})").Take(5).ToList())}
        - weak topics: {JoinOrNone(historySummary.WeakTopics.Select(item => $"{item.Topic} ({item.Accuracy:P0})").Take(5).ToList())}
        - defense summary: {historySummary.DefenseSummary}
        - recent reflections: {JoinOrNone(historySummary.RecentReflections.Take(3).Select(item => $"{item.Mode} {item.Focus}: {item.Reflection}").ToList())}

        Learning profile:
        - current need: {learningProfile.CurrentNeed}
        - active topics: {JoinOrNone(learningProfile.ActiveTopics.Take(6).ToList())}
        - coaching rules: {JoinOrNone(learningProfile.CoachingRules.Take(4).ToList())}
        - imported knowledge: {JoinOrNone(learningLibrary.Documents.Take(4).Select(document => document.PromptSummary).ToList())}
        - relevant notebook evidence:
        {knowledgeContext}

        Study tracks:
        - {JoinOrNone(studyTracks.Select(track => track.Title).ToList())}
        """;
    }

    private static string BuildScoringSystemPrompt() =>
        """
        You score written oral-defense answers for an aspiring electrical engineer.
        Return strict JSON only.
        Score exactly these five rubric items from 0 to 4:
        - Technical Correctness
        - Tradeoff Reasoning
        - Failure-Mode Awareness
        - Validation Thinking
        - Clarity
        Also return:
        - summary
        - nextReviewRecommendation
        - recommendedFollowUps (2 to 4 items)
        Keep feedback direct, specific, and useful.
        """;

    private static string BuildScoringUserPrompt(
        OralDefenseScenario scenario,
        string answer,
        SuiteSnapshot snapshot,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary
    )
    {
        var knowledgeContext = KnowledgePromptContextBuilder.BuildRelevantContext(
            learningLibrary,
            new[]
            {
                scenario.Topic,
                scenario.Title,
                scenario.Prompt,
                answer,
                learningProfile.CurrentNeed,
            },
            maxDocuments: 2,
            maxTotalCharacters: 1800,
            maxExcerptCharacters: 600
        );

        return $"""
        Score this written oral-defense answer.

        Scenario title: {scenario.Title}
        Scenario topic: {scenario.Topic}
        Scenario prompt: {scenario.Prompt}
        What good looks like: {scenario.WhatGoodLooksLike}
        Suite connection: {scenario.SuiteConnection}

        User answer:
        {answer}

        Current Suite context:
        - hot areas: {JoinOrNone(snapshot.HotAreas)}
        - next session tasks: {JoinOrNone(snapshot.NextSessionTasks.Take(4).ToList())}

        Learning profile:
        - current need: {learningProfile.CurrentNeed}
        - active topics: {JoinOrNone(learningProfile.ActiveTopics.Take(6).ToList())}
        - imported knowledge: {JoinOrNone(learningLibrary.Documents.Take(3).Select(document => document.PromptSummary).ToList())}
        - relevant notebook evidence:
        {knowledgeContext}
        """;
    }

    private OralDefenseScenario? ConvertContract(
        OralDefenseContract? contract,
        string? preferredTopic,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile
    )
    {
        if (contract is null
            || string.IsNullOrWhiteSpace(contract.Title)
            || string.IsNullOrWhiteSpace(contract.Prompt)
            || string.IsNullOrWhiteSpace(contract.WhatGoodLooksLike))
        {
            return null;
        }

        var followUps = contract.FollowUpQuestions?
            .Where(question => !string.IsNullOrWhiteSpace(question))
            .Select(question => question!.Trim())
            .Take(5)
            .ToList();

        return new OralDefenseScenario
        {
            Topic = ResolveTopic(contract, preferredTopic, historySummary, learningProfile),
            Title = contract.Title.Trim(),
            Prompt = contract.Prompt.Trim(),
            WhatGoodLooksLike = contract.WhatGoodLooksLike.Trim(),
            SuiteConnection = string.IsNullOrWhiteSpace(contract.SuiteConnection)
                ? "Tie the explanation back to operator trust, review gates, or production reliability in Suite."
                : contract.SuiteConnection.Trim(),
            GenerationSource = $"ollama via {_model}",
            FollowUpQuestions = followUps ?? [],
        };
    }

    private static OralDefenseScenario BuildFallbackScenario(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        string? preferredTopic
    )
    {
        var reviewTarget = historySummary.ReviewRecommendations.FirstOrDefault();
        var weakestTopic = historySummary.WeakTopics.FirstOrDefault()?.Topic;
        var targetTopic = preferredTopic
            ?? reviewTarget?.Topic
            ?? weakestTopic
            ?? learningProfile.ActiveTopics.FirstOrDefault()
            ?? "electrical production judgment";
        var repoTie = snapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot";
        var knowledgeTie = learningLibrary.Documents.FirstOrDefault()?.RelativePath ?? "your imported notes";

        return new OralDefenseScenario
        {
            Topic = targetTopic,
            Title = $"Oral Defense: {targetTopic} applied to {repoTie}",
            Prompt =
                $"Defend how {targetTopic} should influence the next decision in {repoTie}. Explain the governing principle, the main tradeoff, the failure mode you are trying to avoid, and the validation step you would require before trusting the result.",
            WhatGoodLooksLike =
                $"A strong answer ties {targetTopic} to a real engineering consequence, references a concrete tradeoff, names an operator or safety risk, and uses {knowledgeTie} or prior study as evidence instead of speaking only in abstractions.",
            SuiteConnection =
                $"Connect the answer to operator trust, review-first automation, or production reliability in {repoTie}.",
            GenerationSource = "fallback oral drill",
            FollowUpQuestions =
            [
                $"What changes if the review gate is removed from {repoTie}?",
                $"Which wrong assumption would create the highest downstream risk here?",
                $"What evidence would convince another engineer that your approach is safe enough to ship?",
                $"If this became a paid Suite workflow later, what proof of reliability would a customer expect?",
            ],
        };
    }

    private static string ResolveTopic(
        OralDefenseContract contract,
        string? preferredTopic,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile
    )
    {
        if (!string.IsNullOrWhiteSpace(contract.Topic))
        {
            return contract.Topic.Trim();
        }

        return preferredTopic
            ?? historySummary.ReviewRecommendations.FirstOrDefault()?.Topic
            ?? historySummary.WeakTopics.FirstOrDefault()?.Topic
            ?? learningProfile.ActiveTopics.FirstOrDefault()
            ?? "electrical production judgment";
    }

    private static DefenseEvaluation? ConvertEvaluation(DefenseEvaluationContract? contract)
    {
        if (contract?.RubricItems is null || contract.RubricItems.Count < 5)
        {
            return null;
        }

        var items = new List<DefenseRubricItem>();
        foreach (var item in contract.RubricItems.Take(5))
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                return null;
            }

            var score = Math.Clamp(item.Score, 0, 4);
            items.Add(
                new DefenseRubricItem
                {
                    Name = item.Name.Trim(),
                    Score = score,
                    Feedback = string.IsNullOrWhiteSpace(item.Feedback)
                        ? "Explain this area more concretely."
                        : item.Feedback.Trim(),
                }
            );
        }

        return new DefenseEvaluation
        {
            Summary = string.IsNullOrWhiteSpace(contract.Summary)
                ? "Model scored the oral-defense answer."
                : contract.Summary.Trim(),
            NextReviewRecommendation = string.IsNullOrWhiteSpace(contract.NextReviewRecommendation)
                ? "Revisit the weakest rubric area before the next defense."
                : contract.NextReviewRecommendation.Trim(),
            TotalScore = items.Sum(item => item.Score),
            MaxScore = items.Sum(item => item.MaxScore),
            RubricItems = items,
            RecommendedFollowUps = contract.RecommendedFollowUps?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .Take(4)
                .ToList() ?? [],
        };
    }

    private static DefenseEvaluation BuildFallbackEvaluation(
        OralDefenseScenario scenario,
        string answer
    )
    {
        var normalized = answer.Trim();
        var lower = normalized.ToLowerInvariant();
        var longEnough = normalized.Length >= 220;
        var mentionsTradeoff = lower.Contains("tradeoff") || lower.Contains("compromise");
        var mentionsFailure = lower.Contains("failure") || lower.Contains("fault") || lower.Contains("risk");
        var mentionsValidation =
            lower.Contains("validate")
            || lower.Contains("test")
            || lower.Contains("verify")
            || lower.Contains("check");
        var mentionsTechnical =
            lower.Contains("ground")
            || lower.Contains("protection")
            || lower.Contains("standard")
            || lower.Contains("voltage")
            || lower.Contains("operator");

        var items = new List<DefenseRubricItem>
        {
            new()
            {
                Name = "Technical Correctness",
                Score = mentionsTechnical ? 3 : 2,
                Feedback = mentionsTechnical
                    ? "The answer references domain-specific concepts instead of staying generic."
                    : "Add more explicit electrical or operational principles."
            },
            new()
            {
                Name = "Tradeoff Reasoning",
                Score = mentionsTradeoff ? 3 : 1,
                Feedback = mentionsTradeoff
                    ? "A tradeoff is named, which makes the reasoning more credible."
                    : "State the tradeoff instead of only describing the desired outcome."
            },
            new()
            {
                Name = "Failure-Mode Awareness",
                Score = mentionsFailure ? 3 : 1,
                Feedback = mentionsFailure
                    ? "The answer names downstream risk or a failure path."
                    : "Name the failure mode you are trying to avoid."
            },
            new()
            {
                Name = "Validation Thinking",
                Score = mentionsValidation ? 3 : 1,
                Feedback = mentionsValidation
                    ? "The answer includes a validation or review step."
                    : "Describe how you would validate the decision before trusting it."
            },
            new()
            {
                Name = "Clarity",
                Score = longEnough ? 3 : 2,
                Feedback = longEnough
                    ? "The answer is long enough to develop an argument."
                    : "Expand the response so each claim is supported."
            },
        };

        return new DefenseEvaluation
        {
            Summary =
                $"Fallback rubric score for {scenario.Title}. This is a heuristic local score because the mentor model did not return a valid evaluation.",
            NextReviewRecommendation =
                "Tighten the weakest rubric area, then re-answer the same drill before moving on.",
            TotalScore = items.Sum(item => item.Score),
            MaxScore = items.Sum(item => item.MaxScore),
            RubricItems = items,
            RecommendedFollowUps =
            [
                "Where is the tradeoff in your answer?",
                "What failure mode are you trying to prevent?",
                "How would you validate this decision before trusting it?",
            ],
        };
    }

    private static string JoinOrNone(IReadOnlyList<string> items) =>
        items.Count == 0 ? "none recorded" : string.Join("; ", items);

    private sealed class OralDefenseContract
    {
        public string? Topic { get; set; }
        public string? Title { get; set; }
        public string? Prompt { get; set; }
        public string? WhatGoodLooksLike { get; set; }
        public string? SuiteConnection { get; set; }
        public List<string>? FollowUpQuestions { get; set; }
    }

    private sealed class DefenseEvaluationContract
    {
        public string? Summary { get; set; }
        public string? NextReviewRecommendation { get; set; }
        public List<DefenseRubricItemContract>? RubricItems { get; set; }
        public List<string>? RecommendedFollowUps { get; set; }
    }

    private sealed class DefenseRubricItemContract
    {
        public string? Name { get; set; }
        public int Score { get; set; }
        public string? Feedback { get; set; }
    }
}
