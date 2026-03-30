using System.Collections.ObjectModel;

namespace DailyDesk.Models;

public sealed class GuideSection
{
    public string Title { get; set; } = string.Empty;
    public string AccentBrush { get; set; } = "#D7AC69";
    public string AccentSoftBrush { get; set; } = "#F1D7AE";
    public string BodyBrush { get; set; } = "#D6DEE9";
    public string BorderBrush { get; set; } = "#7E6440";
    public string BackgroundBrush { get; set; } = "#111823";
    public ObservableCollection<GuideSectionBlock> Blocks { get; } = [];
}

public sealed class GuideSectionBlock
{
    public string Text { get; set; } = string.Empty;
    public string Foreground { get; set; } = "#D6DEE9";
    public string Background { get; set; } = "Transparent";
    public string BorderBrush { get; set; } = "Transparent";
    public string BorderThickness { get; set; } = "0";
    public string CornerRadius { get; set; } = "0";
    public string Padding { get; set; } = "0";
    public string Margin { get; set; } = "0,0,0,10";
    public string FontFamily { get; set; } = "Plus Jakarta Sans";
    public double FontSize { get; set; } = 14;
    public string FontWeight { get; set; } = "Normal";
}
