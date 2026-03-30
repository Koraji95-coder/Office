using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using DailyDesk.Models;

namespace DailyDesk.ViewModels;

public sealed partial class MainViewModel
{
    private static readonly (string Accent, string AccentSoft, string Body, string Border, string Background)[] GuidePalette =
    [
        ("#D7AC69", "#F1D7AE", "#DCE5F1", "#7E6440", "#111823"),
        ("#7EC7B1", "#CBEFE4", "#D9E8E2", "#3D7B69", "#0F1817"),
        ("#89A7FF", "#D6E0FF", "#D8E1F6", "#4B5FA6", "#101521"),
        ("#D98D78", "#F6D0C5", "#E9DCD7", "#8E584A", "#181312"),
        ("#A88EE6", "#E1D6FF", "#E2DCF4", "#695A99", "#141222"),
        ("#69C7D7", "#C3EDF4", "#D8EAF0", "#3B7480", "#0E181B"),
    ];

    private string _agentReplyGuideTitle = "Agent Reply Guide";
    private string _agentReplyGuideSummary =
        "How to prompt desks, approve work, and get better study and research results.";
    private string _agentReplyGuideText = "Guide not loaded yet.";
    private string _agentReplyGuidePathSummary = "Guide file not loaded yet.";

    public ObservableCollection<GuideSection> AgentReplyGuideSections { get; } = [];

    public string AgentReplyGuideTitle
    {
        get => _agentReplyGuideTitle;
        private set => SetProperty(ref _agentReplyGuideTitle, value);
    }

    public string AgentReplyGuideSummary
    {
        get => _agentReplyGuideSummary;
        private set => SetProperty(ref _agentReplyGuideSummary, value);
    }

    public string AgentReplyGuideText
    {
        get => _agentReplyGuideText;
        private set => SetProperty(ref _agentReplyGuideText, value);
    }

    public string AgentReplyGuidePathSummary
    {
        get => _agentReplyGuidePathSummary;
        private set => SetProperty(ref _agentReplyGuidePathSummary, value);
    }

    private void LoadAgentReplyGuide()
    {
        var guidePath = ResolveAgentReplyGuidePath();
        if (!File.Exists(guidePath))
        {
            AgentReplyGuideTitle = "Agent Reply Guide";
            AgentReplyGuideSummary =
                "The local reply guide was not found. Rebuild DailyDesk or restore AGENT_REPLY_GUIDE.md.";
            AgentReplyGuideText =
                "The guide file is missing from the current build output.\n\nExpected file:\n"
                + guidePath;
            AgentReplyGuidePathSummary = guidePath;
            Replace(
                AgentReplyGuideSections,
                [
                    new GuideSection
                    {
                        Title = "Guide Missing",
                        AccentBrush = "#D98D78",
                        AccentSoftBrush = "#F6D0C5",
                        BodyBrush = "#E9DCD7",
                        BorderBrush = "#8E584A",
                        BackgroundBrush = "#181312",
                        Blocks =
                        {
                            new GuideSectionBlock
                            {
                                Text = AgentReplyGuideText,
                                Foreground = "#E9DCD7",
                            },
                        },
                    },
                ]
            );
            return;
        }

        var guideText = File.ReadAllText(guidePath);
        AgentReplyGuideTitle = "Agent Reply Guide";
        AgentReplyGuideSummary =
            "How to reply to desks, what each desk is good for, and what approval actions actually do.";
        AgentReplyGuideText = NormalizeGuideMarkdownForDisplay(guideText);
        AgentReplyGuidePathSummary = guidePath;
        Replace(AgentReplyGuideSections, ParseGuideSections(guideText));
    }

    private static string ResolveAgentReplyGuidePath()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "AGENT_REPLY_GUIDE.md");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "AGENT_REPLY_GUIDE.md")
        );
    }

    private static string NormalizeGuideMarkdownForDisplay(string markdown)
    {
        var builder = new StringBuilder();
        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (rawLine.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            var line = rawLine;
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                line = line.TrimStart('#').TrimStart();
            }

            builder.AppendLine(line);
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyList<GuideSection> ParseGuideSections(string markdown)
    {
        var sections = new List<GuideSection>();
        GuideSection? currentSection = null;
        var paragraph = new List<string>();
        var codeBlock = new List<string>();
        var inCodeFence = false;
        var paletteIndex = 0;

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (rawLine.StartsWith("# ", StringComparison.Ordinal))
            {
                continue;
            }

            if (rawLine.StartsWith("```", StringComparison.Ordinal))
            {
                if (!inCodeFence)
                {
                    FlushParagraph(currentSection, paragraph);
                    inCodeFence = true;
                    codeBlock.Clear();
                }
                else
                {
                    AddCodeBlock(currentSection, codeBlock);
                    inCodeFence = false;
                }

                continue;
            }

            if (inCodeFence)
            {
                codeBlock.Add(rawLine);
                continue;
            }

            if (rawLine.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushParagraph(currentSection, paragraph);
                currentSection = CreateSection(rawLine[3..].Trim(), paletteIndex++);
                sections.Add(currentSection);
                continue;
            }

            if (currentSection is null)
            {
                continue;
            }

            if (rawLine.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushParagraph(currentSection, paragraph);
                currentSection.Blocks.Add(
                    new GuideSectionBlock
                    {
                        Text = rawLine[4..].Trim(),
                        Foreground = currentSection.AccentSoftBrush,
                        FontFamily = "IBM Plex Mono",
                        FontSize = 12,
                        FontWeight = "SemiBold",
                        Margin = "0,6,0,8",
                    }
                );
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                FlushParagraph(currentSection, paragraph);
                continue;
            }

            paragraph.Add(rawLine);
        }

        FlushParagraph(currentSection, paragraph);

        return sections.Count > 0
            ? sections
            : [CreateFallbackSection(markdown)];
    }

    private static GuideSection CreateSection(string title, int paletteIndex)
    {
        var palette = GuidePalette[paletteIndex % GuidePalette.Length];
        return new GuideSection
        {
            Title = title,
            AccentBrush = palette.Accent,
            AccentSoftBrush = palette.AccentSoft,
            BodyBrush = palette.Body,
            BorderBrush = palette.Border,
            BackgroundBrush = palette.Background,
        };
    }

    private static GuideSection CreateFallbackSection(string markdown)
    {
        var section = CreateSection("Guide", 0);
        section.Blocks.Add(
            new GuideSectionBlock
            {
                Text = NormalizeGuideMarkdownForDisplay(markdown),
                Foreground = section.BodyBrush,
            }
        );
        return section;
    }

    private static void FlushParagraph(GuideSection? section, List<string> paragraph)
    {
        if (section is null || paragraph.Count == 0)
        {
            paragraph.Clear();
            return;
        }

        section.Blocks.Add(
            new GuideSectionBlock
            {
                Text = string.Join("\n", paragraph).Trim(),
                Foreground = section.BodyBrush,
            }
        );
        paragraph.Clear();
    }

    private static void AddCodeBlock(GuideSection? section, List<string> lines)
    {
        if (section is null || lines.Count == 0)
        {
            lines.Clear();
            return;
        }

        section.Blocks.Add(
            new GuideSectionBlock
            {
                Text = string.Join("\n", lines).TrimEnd(),
                Foreground = section.AccentSoftBrush,
                Background = "#0D1218",
                BorderBrush = section.BorderBrush,
                BorderThickness = "1",
                CornerRadius = "12",
                Padding = "12",
                Margin = "0,2,0,12",
                FontFamily = "IBM Plex Mono",
                FontSize = 12,
            }
        );
        lines.Clear();
    }
}
