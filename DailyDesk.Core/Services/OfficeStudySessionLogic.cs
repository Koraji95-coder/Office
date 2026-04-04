using DailyDesk.Models;

namespace DailyDesk.Services;

public static class OfficeStudySessionLogic
{
    public static TrainingSessionStage ResolveStage(OfficeLiveSessionState state)
    {
        if (state.ReflectionSaved)
        {
            return TrainingSessionStage.Complete;
        }

        if (state.DefenseScored)
        {
            return TrainingSessionStage.Reflection;
        }

        if (state.PracticeScored || state.DefenseGenerated)
        {
            return TrainingSessionStage.Defense;
        }

        if (state.PracticeGenerated)
        {
            return TrainingSessionStage.Practice;
        }

        return TrainingSessionStage.Plan;
    }

    public static string BuildStageSummary(TrainingSessionStage stage)
    {
        return stage switch
        {
            TrainingSessionStage.Plan =>
                "Choose the focus, difficulty, and question count. Starting from a review target will bias both practice and defense.",
            TrainingSessionStage.Practice =>
                "Practice is active. Answer the current question set, then score it to unlock the defense stage.",
            TrainingSessionStage.Defense =>
                "Run the oral defense on the same topic so the desk can test explanation quality and tradeoff reasoning.",
            TrainingSessionStage.Reflection =>
                "Capture what felt weak, what to review next, and how this ties back to Suite or career progress.",
            _ =>
                "This session is complete. The history file has been updated and the next review targets are ready.",
        };
    }
}
