using DailyDesk.ViewModels;

namespace DailyDesk.Models;

public sealed class TrainingQuestion : ObservableObject
{
    private string _selectedOptionKey = string.Empty;
    private string _resultText = string.Empty;

    public string Topic { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public IReadOnlyList<TrainingOption> Options { get; init; } = Array.Empty<TrainingOption>();
    public string CorrectOptionKey { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public string SuiteConnection { get; init; } = string.Empty;

    public string SelectedOptionKey
    {
        get => _selectedOptionKey;
        set => SetProperty(ref _selectedOptionKey, value);
    }

    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    public string MetaSummary => $"{Topic} | {Difficulty}";
}
