using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace DailyDesk.Services.Agents;

/// <summary>
/// ML Engineer agent — helps the operator understand and improve their machine
/// learning pipeline.  Tools: get analytics, get forecast, get embeddings status.
/// </summary>
public sealed class MLEngineerAgent : DeskAgent
{
    public MLEngineerAgent(Kernel kernel, ILogger? logger = null)
        : base(kernel, logger) { }

    public override string RouteId => OfficeRouteCatalog.MLRoute;
    public override string Title => "ML Engineer";

    public override string SystemPrompt =>
        PromptComposer.BuildMLEngineerSystemPrompt();

    /// <summary>
    /// Returns a summary of the latest ML analytics results.
    /// </summary>
    [KernelFunction("get_ml_analytics")]
    [System.ComponentModel.Description("Get the latest ML analytics results including weak topics, clusters, and overall readiness.")]
    public static string GetMLAnalytics(
        [System.ComponentModel.Description("Analytics engine name")] string engine,
        [System.ComponentModel.Description("Overall readiness percentage")] string readiness,
        [System.ComponentModel.Description("Comma-separated weak topics")] string weakTopics,
        [System.ComponentModel.Description("Comma-separated topic clusters")] string clusters)
    {
        return $"Engine: {engine}\nReadiness: {readiness}\nWeak topics: {weakTopics}\nClusters: {clusters}";
    }

    /// <summary>
    /// Returns a summary of the latest ML forecast results.
    /// </summary>
    [KernelFunction("get_ml_forecast")]
    [System.ComponentModel.Description("Get the latest ML progress forecast including plateaus and anomalies.")]
    public static string GetMLForecast(
        [System.ComponentModel.Description("Forecast engine name")] string engine,
        [System.ComponentModel.Description("Comma-separated plateau detections")] string plateaus,
        [System.ComponentModel.Description("Comma-separated anomaly alerts")] string anomalies)
    {
        return $"Engine: {engine}\nPlateaus: {plateaus}\nAnomalies: {anomalies}";
    }

    /// <summary>
    /// Returns a summary of the latest ML embeddings results.
    /// </summary>
    [KernelFunction("get_ml_embeddings")]
    [System.ComponentModel.Description("Get the latest ML document embeddings status.")]
    public static string GetMLEmbeddings(
        [System.ComponentModel.Description("Embeddings engine name")] string engine,
        [System.ComponentModel.Description("Number of documents embedded")] string documentCount,
        [System.ComponentModel.Description("Embedding dimension")] string dimension)
    {
        return $"Engine: {engine}\nDocuments: {documentCount}\nDimension: {dimension}";
    }

    /// <summary>
    /// Returns ML pipeline status overview.
    /// </summary>
    [KernelFunction("get_ml_pipeline_status")]
    [System.ComponentModel.Description("Get overall ML pipeline status and last run timestamp.")]
    public static string GetMLPipelineStatus(
        [System.ComponentModel.Description("Whether the ML pipeline has run")] string hasRun,
        [System.ComponentModel.Description("Last ML run timestamp or 'never'")] string lastRunAt)
    {
        return $"Pipeline has run: {hasRun}\nLast run: {lastRunAt}";
    }
}
