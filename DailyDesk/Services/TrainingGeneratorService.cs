using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class TrainingGeneratorService
{
    private readonly IModelProvider _modelProvider;
    private readonly string _model;

    public TrainingGeneratorService(IModelProvider modelProvider, string model)
    {
        _modelProvider = modelProvider;
        _model = model;
    }

    public async Task<PracticeTest> CreatePracticeTestAsync(
        string focus,
        string difficulty,
        int questionCount,
        SuiteSnapshot snapshot,
        TrainingHistorySummary trainingHistory,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        IReadOnlyList<StudyTrack> studyTracks,
        CancellationToken cancellationToken = default
    )
    {
        var safeCount = Math.Clamp(questionCount, 3, 15);

        try
        {
            var generated = await _modelProvider.GenerateJsonAsync<PracticeTestContract>(
                _model,
                BuildSystemPrompt(safeCount),
                BuildUserPrompt(
                    focus,
                    difficulty,
                    snapshot,
                    trainingHistory,
                    learningProfile,
                    learningLibrary,
                    studyTracks
                ),
                cancellationToken
            );

            var converted = ConvertContract(generated, focus, difficulty, safeCount);
            if (converted is not null)
            {
                return converted;
            }
        }
        catch
        {
            // Fall back to local templates when the model fails or returns invalid JSON.
        }

        return BuildFallbackPracticeTest(
            focus,
            difficulty,
            safeCount,
            snapshot,
            trainingHistory,
            learningProfile
        );
    }

    private static string BuildSystemPrompt(int questionCount) =>
        PromptComposer.BuildPracticeTestSystemPrompt(questionCount);

    private static string BuildUserPrompt(
        string focus,
        string difficulty,
        SuiteSnapshot snapshot,
        TrainingHistorySummary trainingHistory,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        IReadOnlyList<StudyTrack> studyTracks
    )
    {
        var knowledgeContext = KnowledgePromptContextBuilder.BuildRelevantContext(
            learningLibrary,
            new[]
            {
                focus,
                difficulty,
                learningProfile.CurrentNeed,
                trainingHistory.ReviewQueueSummary,
                trainingHistory.DefenseSummary,
            },
            maxDocuments: 3,
            maxTotalCharacters: 2400,
            maxExcerptCharacters: 720
        );

        return $"""
        Build a practice test tailored to this focus: {focus}
        Desired difficulty: {difficulty}

        Current Suite context:
        - hot areas: {JoinOrNone(snapshot.HotAreas)}
        - next session tasks: {JoinOrNone(snapshot.NextSessionTasks.Take(4).ToList())}
        - monetization leads: {JoinOrNone(snapshot.MonetizationMoves.Take(4).ToList())}

        Current training history:
        - weak topics: {JoinOrNone(trainingHistory.WeakTopics.Select(topic => $"{topic.Topic} ({topic.Accuracy:P0})").ToList())}
        - recent attempts: {JoinOrNone(trainingHistory.RecentAttempts.Take(4).Select(attempt => attempt.DisplaySummary).ToList())}
        - review queue: {trainingHistory.ReviewQueueSummary}
        - defense summary: {trainingHistory.DefenseSummary}
        - recent reflections: {JoinOrNone(trainingHistory.RecentReflections.Take(3).Select(item => $"{item.Mode} {item.Focus}: {item.Reflection}").ToList())}

        Current learning profile:
        - summary: {learningProfile.Summary}
        - current need: {learningProfile.CurrentNeed}
        - coaching rules: {JoinOrNone(learningProfile.CoachingRules.Take(4).ToList())}
        - imported knowledge: {JoinOrNone(learningLibrary.Documents.Take(4).Select(document => document.PromptSummary).ToList())}
        - relevant notebook evidence:
        {knowledgeContext}

        Current study tracks:
        - {JoinOrNone(studyTracks.Select(track => track.Title).ToList())}
        """;
    }

    private PracticeTest? ConvertContract(
        PracticeTestContract? contract,
        string focus,
        string difficulty,
        int questionCount
    )
    {
        if (contract?.Questions is null || contract.Questions.Count < questionCount)
        {
            return null;
        }

        var questions = new List<TrainingQuestion>();
        foreach (var question in contract.Questions.Take(questionCount))
        {
            if (string.IsNullOrWhiteSpace(question.Prompt)
                || string.IsNullOrWhiteSpace(question.CorrectOptionKey)
                || question.Options is null
                || question.Options.Count < 4)
            {
                return null;
            }

            var options = question.Options
                .Take(4)
                .Select((option, index) => new TrainingOption
                {
                    Key = string.IsNullOrWhiteSpace(option.Key)
                        ? ((char)('A' + index)).ToString()
                        : option.Key.Trim().ToUpperInvariant(),
                    Text = option.Text?.Trim() ?? string.Empty,
                })
                .ToList();

            if (options.Any(option => string.IsNullOrWhiteSpace(option.Text)))
            {
                return null;
            }

            var correctKey = question.CorrectOptionKey.Trim().ToUpperInvariant();
            if (!options.Any(option => option.Key.Equals(correctKey, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            questions.Add(
                new TrainingQuestion
                {
                    Topic = string.IsNullOrWhiteSpace(question.Topic) ? "Electrical reasoning" : question.Topic.Trim(),
                    Difficulty = string.IsNullOrWhiteSpace(question.Difficulty) ? difficulty : question.Difficulty.Trim(),
                    Prompt = question.Prompt.Trim(),
                    Options = options,
                    CorrectOptionKey = correctKey,
                    Explanation = string.IsNullOrWhiteSpace(question.Explanation)
                        ? "Review the governing principle and explain why the correct option best matches the engineering intent."
                        : question.Explanation.Trim(),
                    SuiteConnection = string.IsNullOrWhiteSpace(question.SuiteConnection)
                        ? "Tie the answer back to operator trust, review gates, or production reliability in Suite."
                        : question.SuiteConnection.Trim(),
                }
            );
        }

        return new PracticeTest
        {
            Title = string.IsNullOrWhiteSpace(contract.Title)
                ? $"Practice Test: {focus}"
                : contract.Title.Trim(),
            Overview = string.IsNullOrWhiteSpace(contract.Overview)
                ? "Generated local practice test."
                : contract.Overview.Trim(),
            Focus = focus,
            Difficulty = difficulty,
            GenerationSource = $"{_modelProvider.ProviderId}:{questionCount} via {_model}",
            Questions = questions,
        };
    }

    private static PracticeTest BuildFallbackPracticeTest(
        string focus,
        string difficulty,
        int questionCount,
        SuiteSnapshot snapshot,
        TrainingHistorySummary trainingHistory,
        LearningProfile learningProfile
    )
    {
        var focusTerms = focus
            .Split([',', ';', '/', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.ToLowerInvariant())
            .ToList();

        var selected = FallbackQuestions
            .OrderByDescending(
                template =>
                    ScoreTemplate(
                        template,
                        focusTerms,
                        difficulty,
                        trainingHistory,
                        learningProfile
                    )
            )
            .ThenBy(_ => Random.Shared.Next())
            .Take(questionCount)
            .Select(template => new TrainingQuestion
            {
                Topic = template.Topic,
                Difficulty = template.Difficulty,
                Prompt = template.Prompt,
                Options = template.Options
                    .Select((option, index) => new TrainingOption
                    {
                        Key = ((char)('A' + index)).ToString(),
                        Text = option,
                    })
                    .ToList(),
                CorrectOptionKey = ((char)('A' + template.CorrectIndex)).ToString(),
                Explanation = template.Explanation,
                SuiteConnection = template.SuiteConnection,
            })
            .ToList();

        return new PracticeTest
        {
            Title = $"Practice Test: {focus}",
            Overview =
                $"Fallback training set tailored to {focus}. Suite context: {snapshot.StatusSummary} Weak-topic steer: {JoinOrNone(trainingHistory.WeakTopics.Select(topic => topic.Topic).Take(3).ToList())}. Profile steer: {JoinOrNone(learningProfile.ActiveTopics.Take(4).ToList())}.",
            Focus = focus,
            Difficulty = difficulty,
            GenerationSource = "fallback library",
            Questions = selected,
        };
    }

    private static int ScoreTemplate(
        FallbackQuestionTemplate template,
        IReadOnlyList<string> focusTerms,
        string difficulty,
        TrainingHistorySummary trainingHistory,
        LearningProfile learningProfile
    )
    {
        var score = 0;
        foreach (var term in focusTerms)
        {
            if (template.Topic.Contains(term, StringComparison.OrdinalIgnoreCase)
                || template.Prompt.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 4;
            }
        }

        if (difficulty.Equals("Mixed", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }
        else if (template.Difficulty.Equals(difficulty, StringComparison.OrdinalIgnoreCase))
        {
            score += 3;
        }

        foreach (var weakTopic in trainingHistory.WeakTopics.Take(3))
        {
            if (template.Topic.Contains(weakTopic.Topic, StringComparison.OrdinalIgnoreCase)
                || template.Prompt.Contains(weakTopic.Topic, StringComparison.OrdinalIgnoreCase)
                || weakTopic.Topic.Contains(template.Topic, StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
            }
        }

        foreach (var activeTopic in learningProfile.ActiveTopics.Take(4))
        {
            if (template.Topic.Contains(activeTopic, StringComparison.OrdinalIgnoreCase)
                || template.Prompt.Contains(activeTopic, StringComparison.OrdinalIgnoreCase)
                || activeTopic.Contains(template.Topic, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }
        }

        return score;
    }

    private static string JoinOrNone(IReadOnlyList<string> items) =>
        items.Count == 0 ? "none recorded" : string.Join("; ", items);

    private sealed class PracticeTestContract
    {
        public string? Title { get; set; }
        public string? Overview { get; set; }
        public List<PracticeQuestionContract>? Questions { get; set; }
    }

    private sealed class PracticeQuestionContract
    {
        public string? Topic { get; set; }
        public string? Difficulty { get; set; }
        public string? Prompt { get; set; }
        public List<PracticeOptionContract>? Options { get; set; }
        public string? CorrectOptionKey { get; set; }
        public string? Explanation { get; set; }
        public string? SuiteConnection { get; set; }
    }

    private sealed class PracticeOptionContract
    {
        public string? Key { get; set; }
        public string? Text { get; set; }
    }

    private sealed record FallbackQuestionTemplate(
        string Topic,
        string Difficulty,
        string Prompt,
        string[] Options,
        int CorrectIndex,
        string Explanation,
        string SuiteConnection
    );

    private static readonly IReadOnlyList<FallbackQuestionTemplate> FallbackQuestions =
    [
        new(
            "Grounding",
            "Fundamental",
            "Why is equipment grounding primarily important in a power system?",
            [
                "It raises normal operating voltage so motors start faster.",
                "It provides a low-impedance fault path to help protective devices clear faults.",
                "It replaces the need for overcurrent protection.",
                "It increases usable real power on lightly loaded feeders.",
            ],
            1,
            "Grounding is there to improve fault clearing and reduce touch-voltage hazard, not to replace protective coordination.",
            "Grounding logic should shape how you think about safe defaults and failure paths in Suite workflows."
        ),
        new(
            "Protection",
            "Intermediate",
            "What is the main engineering purpose of overcurrent protective coordination?",
            [
                "To guarantee every breaker trips at the same time.",
                "To keep harmonics below IEEE 519 limits in all cases.",
                "To isolate the smallest possible faulted section while preserving upstream service.",
                "To eliminate the need for short-circuit studies.",
            ],
            2,
            "Selective coordination is about isolating faults with minimal collateral outage, which is a systems-thinking problem.",
            "This matches Suite's operator-first mindset: localize failures and preserve the rest of the workflow."
        ),
        new(
            "Voltage Drop",
            "Intermediate",
            "If a feeder experiences excessive voltage drop, what is the most direct consequence at the load?",
            [
                "The load receives more available fault current.",
                "The equipment may underperform or overheat because it is not receiving intended terminal voltage.",
                "The feeder automatically becomes selectively coordinated.",
                "Reactive power is eliminated at the load.",
            ],
            1,
            "Voltage drop impacts terminal performance and can push equipment into poor operating conditions.",
            "The same discipline applies in software: degraded inputs often create misleading downstream behavior."
        ),
        new(
            "Standards",
            "Challenging",
            "Why is a standards checker valuable in an electrical production workflow?",
            [
                "Because it lets you skip review entirely when the model is confident.",
                "Because it converts engineering judgment into visible, repeatable checks before release.",
                "Because it guarantees zero design errors after one pass.",
                "Because it removes the need for drawing conventions.",
            ],
            1,
            "A standards checker does not replace engineering judgment; it makes repeatable checks visible and reviewable.",
            "This is directly tied to Suite monetization because trustable, review-first QA is easier to sell than generic AI."
        ),
        new(
            "Drafting Safety",
            "Challenging",
            "Why should CAD automation stay review-first instead of fully autonomous in a production electrical workflow?",
            [
                "Because operators dislike all automation.",
                "Because electrical drawings carry coordination, compliance, and downstream construction risk.",
                "Because review-first is always faster than automation.",
                "Because deterministic tooling can never be useful.",
            ],
            1,
            "Electrical drawings are not just graphics; they encode real-world intent and coordination risk, so review gates matter.",
            "This is a core Suite principle and a strong career talking point for safe automation."
        ),
        new(
            "Power Factor",
            "Fundamental",
            "What is the practical effect of poor power factor in an AC system?",
            [
                "Higher current for the same real power, increasing losses and equipment burden.",
                "Lower short-circuit current at every bus.",
                "Automatic reduction of harmonics without filters.",
                "Guaranteed improvement in voltage regulation.",
            ],
            0,
            "Poor power factor forces more current for the same real work, which drives losses and equipment sizing concerns.",
            "Use this as a reminder to reason from system consequences, not just formulas."
        ),
        new(
            "Per-Unit",
            "Intermediate",
            "Why do engineers use the per-unit system in power-system analysis?",
            [
                "To avoid all transformer modeling assumptions.",
                "To normalize values across different voltage levels and simplify comparison.",
                "To make circuit quantities dimensionless so they no longer require engineering review.",
                "To eliminate the need for base quantities.",
            ],
            1,
            "Per-unit normalizes system values and makes cross-level comparison more manageable, especially in multi-voltage systems.",
            "In software terms, this is similar to normalizing data before building logic on top of it."
        ),
        new(
            "Title Block Control",
            "Intermediate",
            "Why are title block sync receipts important in a drawing-control workflow?",
            [
                "They are mostly decorative and can be skipped once the UI looks correct.",
                "They provide traceability for what changed, where it changed, and whether review occurred.",
                "They remove the need for project metadata.",
                "They guarantee all project files are on the correct server.",
            ],
            1,
            "Receipts turn an action into something inspectable and supportable, which matters for production trust.",
            "That traceability is part of what makes Suite's production-control angle monetizable later."
        ),
        new(
            "Telemetry",
            "Fundamental",
            "Why does noisy telemetry reduce the value of production monitoring?",
            [
                "Because more data always lowers accuracy.",
                "Because noise hides the events that actually matter to operators and managers.",
                "Because telemetry should never include repeated events.",
                "Because dashboards should only show failures.",
            ],
            1,
            "Noisy telemetry drowns signal, making it harder to trust alerts and summaries when they matter.",
            "This aligns with Suite's Watchdog monetization needs: cleaner event vocabulary and stronger rollups."
        ),
        new(
            "Production Workflow",
            "Challenging",
            "What is the strongest first commercial angle for Suite according to the current repo thinking?",
            [
                "Sell a broad all-in-one AI workspace immediately.",
                "Sell autonomous CAD edits with no review to maximize wow factor.",
                "Sell drawing production control for electrical AutoCAD teams with a boringly reliable workflow.",
                "Sell generic project management features first.",
            ],
            2,
            "A narrow, reliable job is easier to trust, explain, support, and measure than a broad AI platform pitch.",
            "This question ties your engineering work directly to business judgment, which is part of the Daily Desk goal."
        ),
        new(
            "Transformers",
            "Intermediate",
            "Which transformer connection is commonly used to block zero-sequence current from propagating between networks?",
            [
                "Delta-delta, because both windings share the same reference.",
                "Wye-wye with a solid neutral on both sides.",
                "Delta-wye, because the delta winding isolates zero-sequence on that side.",
                "Autotransformer, because of its reduced copper losses.",
            ],
            2,
            "A delta winding does not provide a return path for zero-sequence current, effectively isolating ground faults from propagating.",
            "Understanding transformer connections is essential when designing Suite workflows that model protection zones and grounding boundaries."
        ),
        new(
            "Short-Circuit Analysis",
            "Challenging",
            "Why must a short-circuit study be performed before selecting protective devices for a new distribution system?",
            [
                "To determine the aesthetic layout of the single-line diagram.",
                "To verify the available fault current and ensure device interrupting ratings are not exceeded.",
                "To remove the need for coordination studies entirely.",
                "To allow the use of smaller conductor sizes throughout the system.",
            ],
            1,
            "Protective devices must be rated to interrupt the maximum available fault current; exceeding the interrupting rating creates a catastrophic failure risk.",
            "This engineering gate maps directly to Suite's review-first principle: you must validate the system state before authorizing downstream actions."
        ),
        new(
            "Arc Flash",
            "Fundamental",
            "What is the primary purpose of an arc flash hazard analysis?",
            [
                "To eliminate the need for personal protective equipment in a facility.",
                "To determine the incident energy at a work location and select appropriate PPE.",
                "To replace the short-circuit study with a single unified calculation.",
                "To verify that all conductors are oversized for future load growth.",
            ],
            1,
            "Arc flash analysis quantifies the thermal energy released during a fault at a given location, allowing workers to select PPE rated above that threshold.",
            "Safety-critical analysis steps like arc flash review are natural candidates for operator-approval gates in Suite's production workflow."
        ),
        new(
            "Single-Line Diagrams",
            "Fundamental",
            "Why are single-line diagrams the standard reference document for an electrical power system?",
            [
                "Because they show all three phases simultaneously for maximum detail.",
                "Because they provide a simplified, symbolic view of system topology that supports design, analysis, and communication.",
                "Because regulatory bodies require three-line diagrams for all studies.",
                "Because they replace the need for equipment schedules and load lists.",
            ],
            1,
            "Single-line diagrams abstract phase detail to focus on topology, equipment connections, and protective device placement, making them the primary communication tool.",
            "Suite's drawing-production workflows revolve around single-line diagram control, so understanding their purpose is foundational to every workflow gate."
        ),
        new(
            "Load Flow",
            "Intermediate",
            "What does a load flow (power flow) study primarily determine in a power system?",
            [
                "The maximum short-circuit current at every bus.",
                "The steady-state voltages, currents, and power flows throughout the network under expected loading.",
                "The arc flash incident energy at every panel.",
                "The harmonic distortion spectrum across all feeders.",
            ],
            1,
            "Load flow studies solve for the steady-state operating point, revealing voltage profiles and power distribution so engineers can identify overloads and voltage violations.",
            "Steady-state system knowledge is a prerequisite for trustable automation; Suite workflows should be grounded in the same principle of knowing the current state before making changes."
        ),
    ];
}
