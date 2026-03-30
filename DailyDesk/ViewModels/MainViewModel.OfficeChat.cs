using System.Text;
using DailyDesk.Models;
using DailyDesk.Services;

namespace DailyDesk.ViewModels;

public sealed partial class MainViewModel
{
    private async Task SendDeskMessageAsync()
    {
        if (SelectedDesk is null)
        {
            return;
        }

        var prompt = DeskMessageDraft.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        if (prompt.StartsWith("/research ", StringComparison.OrdinalIgnoreCase))
        {
            ResearchQueryText = prompt[10..].Trim();
            SelectedResearchMode = ResolveResearchPerspective(SelectedDesk.Id);
            DeskMessageDraft = string.Empty;
            await RunLiveResearchAsync();
            return;
        }

        var desk = SelectedDesk;
        var deskId = desk.Id;
        var userMessage = new DeskMessageRecord
        {
            DeskId = deskId,
            Role = "user",
            Author = "You",
            Kind = "chat",
            Content = prompt,
            CreatedAt = DateTimeOffset.Now,
        };

        await AppendDeskMessagesAsync(deskId, userMessage);
        DeskMessageDraft = string.Empty;

        var model = ResolveDeskModel(deskId);
        var job = StartJob(
            desk.Name,
            desk.Name,
            model,
            $"Working inside the {desk.Name} thread."
        );
        IsBusy = true;
        StatusMessage = $"Running {desk.Name}...";

        try
        {
            var response = await _ollamaService.GenerateAsync(
                model,
                BuildDeskSystemPrompt(deskId, prompt),
                BuildDeskConversationPrompt(deskId, prompt)
            );
            if (string.IsNullOrWhiteSpace(response))
            {
                response = BuildDeskFallbackResponse(deskId, prompt);
            }

            await AppendDeskMessagesAsync(
                deskId,
                new DeskMessageRecord
                {
                    DeskId = deskId,
                    Role = "assistant",
                    Author = desk.Name,
                    Kind = "chat",
                    Content = response.Trim(),
                    CreatedAt = DateTimeOffset.Now,
                }
            );

            StatusMessage = $"{desk.Name} responded.";
            CompleteJob(job, StatusMessage);
            await RecordActivityAsync(
                CreateActivity("desk_chat", desk.Name, deskId, response)
            );
        }
        catch (Exception exception)
        {
            var fallback = BuildDeskFallbackResponse(deskId, prompt);
            await AppendDeskMessagesAsync(
                deskId,
                new DeskMessageRecord
                {
                    DeskId = deskId,
                    Role = "assistant",
                    Author = desk.Name,
                    Kind = "fallback",
                    Content = fallback,
                    CreatedAt = DateTimeOffset.Now,
                }
            );

            StatusMessage = $"{desk.Name} fell back to a local response: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunDeskActionAsync(DeskAction? action)
    {
        if (action is null || SelectedDesk is null)
        {
            return;
        }

        SelectedResearchMode = ResolveResearchPerspective(SelectedDesk.Id);

        switch (action.Id)
        {
            case "chief.refresh":
                await RefreshContextAsync();
                break;
            case "chief.morning-pass":
                await RunMorningPassAsync();
                break;
            case "chief.daily-plan":
                await GenerateDailyPlanAsync();
                break;
            case "engineering.study_guide":
                await GenerateStudyGuideAsync();
                break;
            case "engineering.challenge":
                await GenerateChallengeAsync();
                break;
            case "engineering.practice":
                await GeneratePracticeTestAsync();
                break;
            case "engineering.defense":
                await GenerateOralDefenseAsync();
                break;
            case "suite.refresh":
                await RefreshContextAsync();
                break;
            case "suite.coach":
            case "suite.context":
                await RunSuiteCoachAsync();
                break;
            case "suite.research":
                if (string.IsNullOrWhiteSpace(ResearchQueryText))
                {
                    ResearchQueryText = "suite developer workflow runtime doctor review-first automation";
                }
                await RunLiveResearchAsync();
                break;
            case "business.map":
                await GenerateMonetizationAsync();
                break;
            case "business.research":
                if (string.IsNullOrWhiteSpace(ResearchQueryText))
                {
                    ResearchQueryText = "electrical production control workflow software pricing pilot market";
                }
                await RunLiveResearchAsync();
                break;
            case "business.plan":
                await GenerateDailyPlanAsync();
                break;
        }
    }

    private async Task AppendDeskMessagesAsync(
        string deskId,
        params DeskMessageRecord[] messages
    )
    {
        var thread = ResolveDeskThread(deskId);
        foreach (var message in messages.Where(item => !string.IsNullOrWhiteSpace(item.Content)))
        {
            thread.Messages.Add(message);
            thread.UpdatedAt = message.CreatedAt;
        }

        thread.Messages = thread.Messages
            .OrderBy(item => item.CreatedAt)
            .TakeLast(120)
            .ToList();

        _operatorMemoryState = await _operatorMemoryStore.SaveDeskThreadsAsync(
            _operatorMemoryState.DeskThreads
        );
        ApplyOperatorState(_operatorMemoryState);
    }

    private async Task AppendDeskOutputAsync(
        string deskId,
        string author,
        string kind,
        string content
    )
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        await AppendDeskMessagesAsync(
            deskId,
            new DeskMessageRecord
            {
                DeskId = deskId,
                Role = "assistant",
                Author = author,
                Kind = kind,
                Content = content.Trim(),
                CreatedAt = DateTimeOffset.Now,
            }
        );
    }

    private DeskThreadState ResolveDeskThread(string deskId)
    {
        var existing = _operatorMemoryState.FindDeskThread(deskId);
        if (existing is not null)
        {
            return existing;
        }

        var created = new DeskThreadState
        {
            DeskId = deskId,
            DeskTitle = ResolveDeskTitle(deskId),
            UpdatedAt = DateTimeOffset.Now,
        };
        _operatorMemoryState.DeskThreads.Add(created);
        return created;
    }

    private string BuildDeskSystemPrompt(string deskId, string userInput)
    {
        var notebookFocused = IsNotebookFocusedRequest(userInput);
        return deskId switch
        {
            ChiefDeskId =>
                """
                You are the Chief of Staff inside Office, a Windows desktop for direct work with a personal team.
                Route the day across Suite, electrical engineering, CAD workflow judgment, and business operations.
                Stay read-only toward Suite.
                Respond with short sections named NEXT MOVE, WHY, and HANDOFF.
                """,
            EngineeringDeskId when notebookFocused =>
                """
                You are the Engineering Desk inside Office.
                Combine electrical engineering teaching, practice-test coaching, and oral-defense reasoning.
                The user is asking for help grounded in imported notebook material.
                Prioritize imported-note evidence over generic background knowledge.
                Do not introduce unrelated Suite, business, workflow-vendor, or market-research references unless the user explicitly asks for them.
                If the notebook evidence is thin, say that plainly.
                Respond with short sections named ANSWER, NOTEBOOK EVIDENCE, and NEXT STEP.
                """,
            EngineeringDeskId =>
                """
                You are the Engineering Desk inside Office.
                Combine electrical engineering teaching, CAD workflow judgment, practice-test coaching, and oral-defense reasoning.
                Keep answers practical, operator-safe, and tied to review-first production work.
                Respond with short sections named ANSWER, CHECKS, and CAD OR SUITE LINK.
                """,
            SuiteDeskId =>
                """
                You are the Suite Context desk inside Office.
                Keep the office aware of Suite trust, availability, and workflow context without turning into a repo-planning tool.
                Stay read-only and avoid implementation proposals unless explicitly asked.
                Respond with short sections named CONTEXT, TRUST, and WHY IT MATTERS.
                """,
            BusinessDeskId =>
                """
                You are Business Ops inside Office.
                Turn current capability into internal operating moves, pilot-shaped offers, and monetization proof without hype.
                Keep the focus on real electrical production-control value.
                Respond with short sections named MOVE, WHY IT WINS, and WHAT TO PROVE.
                """,
            _ =>
                """
                You are a practical assistant inside Office.
                Respond directly and keep the answer tied to action.
                """,
        };
    }

    private string BuildDeskConversationPrompt(string deskId, string userInput)
    {
        var notebookFocused = IsNotebookFocusedRequest(userInput);
        var notebookLibrary = notebookFocused ? BuildNotebookOnlyLibrary() : _learningLibrary;
        var thread = ResolveDeskThread(deskId);
        var history = thread.Messages
            .Where(item => !item.Kind.Equals("system", StringComparison.OrdinalIgnoreCase))
            .TakeLast(8)
            .ToList();
        var knowledgeContext = KnowledgePromptContextBuilder.BuildRelevantContext(
            notebookLibrary,
            new[]
            {
                userInput,
                SelectedDesk?.Name,
                _learningProfile.CurrentNeed,
                string.Join("; ", _learningProfile.ActiveTopics.Take(4)),
                string.Join("; ", _trainingHistorySummary.WeakTopics.Take(4).Select(item => item.Topic)),
            },
            maxDocuments: 2,
            maxTotalCharacters: 1800,
            maxExcerptCharacters: 620
        );

        var builder = new StringBuilder();
        builder.AppendLine("Office operating parameters:");
        builder.AppendLine($"- suite: {_settings.SuiteFocus}");
        builder.AppendLine($"- engineering: {_settings.EngineeringFocus}");
        builder.AppendLine($"- cad: {_settings.CadFocus}");
        builder.AppendLine($"- business: {_settings.BusinessFocus}");
        builder.AppendLine($"- career: {_settings.CareerFocus}");
        builder.AppendLine();
        builder.AppendLine("Current Suite context:");
        builder.AppendLine($"- suite awareness: {BuildQuietSuiteContextSummary(_suiteSnapshot)}");
        builder.AppendLine($"- suite trust: {BuildQuietSuiteTrustSummary(_suiteSnapshot)}");
        builder.AppendLine($"- suite focus: {_settings.SuiteFocus}");
        builder.AppendLine();
        builder.AppendLine("Current engineering and knowledge context:");
        builder.AppendLine($"- learning profile: {_learningProfile.Summary}");
        builder.AppendLine($"- current need: {_learningProfile.CurrentNeed}");
        builder.AppendLine($"- review queue: {_trainingHistorySummary.ReviewQueueSummary}");
        builder.AppendLine($"- defense summary: {_trainingHistorySummary.DefenseSummary}");
        builder.AppendLine($"- imported knowledge: {JoinOrNone(notebookLibrary.Documents.Take(5).Select(item => item.PromptSummary).ToList())}");
        builder.AppendLine("- relevant notebook evidence:");
        builder.AppendLine(knowledgeContext);
        builder.AppendLine();
        if (notebookFocused)
        {
            builder.AppendLine($"- notebook packages loaded: {notebookLibrary.Documents.Count}");
            builder.AppendLine("Notebook-only guidance:");
            builder.AppendLine("- stay grounded in the imported note evidence above");
            builder.AppendLine("- summarize notebook contents before adding outside knowledge");
            builder.AppendLine("- do not introduce unrelated software, vendor, or business recommendations");
            builder.AppendLine("- if the notes are incomplete, call out the gap instead of guessing");
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine("Current business and operator context:");
            builder.AppendLine($"- daily objective: {DailyObjective}");
            builder.AppendLine($"- approval inbox: {ApprovalInboxSummary}");
            builder.AppendLine($"- monetization leads: {JoinOrNone(_suiteSnapshot.MonetizationMoves)}");
            builder.AppendLine($"- recent suggestions: {JoinOrNone(_operatorMemoryState.RecentSuggestions.Take(5).Select(item => item.Title).ToList())}");
            builder.AppendLine();
        }
        builder.AppendLine("Recent desk thread:");
        foreach (var message in history)
        {
            builder.AppendLine($"{message.Author}: {Truncate(message.Content, 420)}");
        }

        builder.AppendLine();
        builder.AppendLine("User request:");
        builder.AppendLine(userInput);
        builder.AppendLine();
        if (notebookFocused)
        {
            builder.AppendLine("Keep the answer notebook-grounded, explicit about what came from the imported notes, and useful for study.");
        }
        else
        {
            builder.AppendLine("Keep the answer action-oriented and grounded in the selected desk's job.");
        }

        return builder.ToString();
    }

    private string BuildDeskFallbackResponse(string deskId, string userInput) =>
        deskId switch
        {
            ChiefDeskId =>
                $"NEXT MOVE\nStart from the current highest-leverage block: {DailyObjective}\n\nWHY\nSuite pressure is {_suiteSnapshot.StatusSummary} and the current training need is {_learningProfile.CurrentNeed}\n\nHANDOFF\nIf this question needs current web facts, use /research {userInput}",
            EngineeringDeskId =>
                $"ANSWER\nTie this back to {_learningProfile.CurrentNeed} and keep the explanation review-first.\n\nCHECKS\nUse the next review target: {_trainingHistorySummary.ReviewRecommendations.FirstOrDefault()?.Topic ?? "your current weak topic"}\n\nCAD OR SUITE LINK\nRelate the answer to {_suiteSnapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot"} and {_settings.CadFocus}",
            SuiteDeskId =>
                $"CONTEXT\n{BuildQuietSuiteContextSummary(_suiteSnapshot)}\n\nTRUST\n{BuildQuietSuiteTrustSummary(_suiteSnapshot)}\n\nWHY IT MATTERS\nUse Suite as background context for better decisions, not as a prompt to start repo work.",
            BusinessDeskId =>
                $"MOVE\nLead with {_suiteSnapshot.MonetizationMoves.FirstOrDefault() ?? "drawing production control for electrical teams"}\n\nWHY IT WINS\nIt stays tied to real operator value instead of generic AI positioning\n\nWHAT TO PROVE\nUse current Suite work and EE/CAD judgment as concrete proof",
            _ => "Work from the current context and choose the next bounded move.",
        };

    private static bool IsNotebookFocusedRequest(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        return normalized.Contains("onenote", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("one note", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(".onepkg", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("notebook", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("imported note", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("study guide", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("quiz me", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("quiz", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("tell me what its contents are", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("tell me what it contains", StringComparison.OrdinalIgnoreCase);
    }
}
