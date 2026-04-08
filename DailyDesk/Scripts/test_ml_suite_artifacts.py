"""
Integration tests for ml_suite_artifacts.py.

Verifies that all code paths produce compliant responses:
  - JSONDecodeError → {"ok": false, "error": "Invalid JSON input."}
  - Unexpected exceptions → {"ok": false, "error": "An unexpected error occurred. See server logs for details."}
  - Happy path → {"ok": true, "artifacts": [...]}
"""

import json
import sys
import unittest
from io import StringIO
from pathlib import Path
from unittest.mock import patch

# ---------------------------------------------------------------------------
# Import the module under test
# ---------------------------------------------------------------------------
_SCRIPTS_DIR = Path(__file__).parent
sys.path.insert(0, str(_SCRIPTS_DIR))

import ml_suite_artifacts as _module  # noqa: E402


# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
_STATIC_UNEXPECTED_ERROR = (
    "An unexpected error occurred. See server logs for details."
)
_STATIC_JSON_ERROR = "Invalid JSON input."

_MINIMAL_VALID_INPUT = json.dumps(
    {
        "analytics": {
            "overallReadiness": 0.8,
            "readinessBreakdown": [],
            "adaptiveSchedule": [],
            "topicClusters": [],
            "operatorPattern": {},
            "engine": "test-engine",
        },
        "embeddings": {
            "embeddings": [],
            "similarities": [],
            "engine": "test-engine",
        },
        "forecast": {
            "masteryEstimates": [],
            "anomalies": [],
            "plateaus": [],
            "forecasts": [],
            "engine": "test-engine",
        },
    }
)

_RICH_VALID_INPUT = json.dumps(
    {
        "analytics": {
            "overallReadiness": 0.9,
            "readinessBreakdown": [
                {
                    "topic": "electrical-safety",
                    "readiness": 0.85,
                    "confidence": 0.9,
                    "trend": 0.88,
                    "improving": True,
                },
                {
                    "topic": "circuit-theory",
                    "readiness": 0.6,
                    "confidence": 0.75,
                    "trend": 0.65,
                    "improving": True,
                },
            ],
            "adaptiveSchedule": [
                {"topic": "electrical-safety", "priority": 1, "hoursPerWeek": 3},
                {"topic": "circuit-theory", "priority": 2, "hoursPerWeek": 5},
            ],
            "topicClusters": [["electrical-safety", "circuit-theory"]],
            "operatorPattern": {"style": "visual", "peakHour": 9},
            "engine": "test-analytics-engine",
        },
        "embeddings": {
            "embeddings": [
                {"documentId": "doc-001", "title": "Electrical Safety Guide", "dimensions": 384},
                {"documentId": "doc-002", "title": "Circuit Theory Fundamentals", "dimensions": 384},
            ],
            "similarities": [
                {"documentA": "doc-001", "documentB": "doc-002", "similarity": 0.72},
            ],
            "engine": "test-embed-engine",
        },
        "forecast": {
            "masteryEstimates": [
                {"topic": "electrical-safety", "mastered": True, "estimatedDays": None},
                {"topic": "circuit-theory", "mastered": False, "estimatedDays": 14},
            ],
            "anomalies": [],
            "plateaus": [
                {"topic": "circuit-theory", "recommendation": "try spaced repetition"},
            ],
            "forecasts": [
                {
                    "topic": "electrical-safety",
                    "currentAccuracy": 0.85,
                    "trend": "improving",
                    "dataPoints": 42,
                },
                {
                    "topic": "circuit-theory",
                    "currentAccuracy": 0.6,
                    "trend": "stable",
                    "dataPoints": 30,
                },
            ],
            "engine": "test-forecast-engine",
        },
    }
)

_LOW_READINESS_INPUT = json.dumps(
    {
        "analytics": {
            "overallReadiness": 0.3,
            "readinessBreakdown": [],
            "adaptiveSchedule": [],
            "topicClusters": [],
            "operatorPattern": {},
            "engine": "test-engine",
        },
        "embeddings": {"embeddings": [], "similarities": [], "engine": "test-engine"},
        "forecast": {
            "masteryEstimates": [],
            "anomalies": [{"topic": "some-topic", "severity": "high"}],
            "plateaus": [],
            "forecasts": [],
            "engine": "test-engine",
        },
    }
)

_MID_READINESS_INPUT = json.dumps(
    {
        "analytics": {
            "overallReadiness": 0.6,
            "readinessBreakdown": [],
            "adaptiveSchedule": [],
            "topicClusters": [],
            "operatorPattern": {},
            "engine": "test-engine",
        },
        "embeddings": {"embeddings": [], "similarities": [], "engine": "test-engine"},
        "forecast": {
            "masteryEstimates": [],
            "anomalies": [],
            "plateaus": [],
            "forecasts": [],
            "engine": "test-engine",
        },
    }
)


# ---------------------------------------------------------------------------
# Helper
# ---------------------------------------------------------------------------
def _run_main_with_input(stdin_text: str) -> dict:
    """Run main() with the given stdin text and return parsed stdout JSON."""
    with patch("sys.stdin", StringIO(stdin_text)):
        captured = StringIO()
        with patch("sys.stdout", captured):
            _module.main()
    output = captured.getvalue().strip()
    return json.loads(output)


# ---------------------------------------------------------------------------
# Group 1: JSONDecodeError path
# ---------------------------------------------------------------------------
class TestJsonDecodeErrorPath(unittest.TestCase):
    """main() must return static 'Invalid JSON input.' on malformed JSON."""

    def test_invalid_json_returns_ok_false(self):
        result = _run_main_with_input("{not valid json}")
        self.assertFalse(result["ok"])

    def test_invalid_json_returns_static_error_string(self):
        result = _run_main_with_input("{not valid json}")
        self.assertEqual(result["error"], _STATIC_JSON_ERROR)

    def test_truncated_json_returns_static_error_string(self):
        result = _run_main_with_input('{"analytics": {')
        self.assertEqual(result["error"], _STATIC_JSON_ERROR)

    def test_bare_text_returns_static_error_string(self):
        result = _run_main_with_input("this is not json at all")
        self.assertEqual(result["error"], _STATIC_JSON_ERROR)

    def test_invalid_json_error_has_no_exception_details(self):
        """The error value must be a static string, not a dynamic exception message."""
        result = _run_main_with_input("{bad}")
        self.assertNotIn("JSONDecodeError", result["error"])
        self.assertNotIn("Expecting", result["error"])
        self.assertNotIn("line", result["error"])


# ---------------------------------------------------------------------------
# Group 2: Unexpected exception paths
# ---------------------------------------------------------------------------
class TestUnexpectedExceptionPath(unittest.TestCase):
    """All non-JSONDecodeError exceptions must return a static error string."""

    def _assert_unexpected_error_response(self, result: dict) -> None:
        self.assertFalse(result["ok"])
        self.assertEqual(result["error"], _STATIC_UNEXPECTED_ERROR)

    def test_key_error_in_artifact_builder_returns_static_detail(self):
        """A KeyError raised inside an artifact builder must produce static detail."""
        with patch.object(_module, "_build_operator_readiness", side_effect=KeyError("topic")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_unexpected_error_response(result)

    def test_type_error_in_artifact_builder_returns_static_detail(self):
        with patch.object(_module, "_build_knowledge_index", side_effect=TypeError("bad type")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_unexpected_error_response(result)

    def test_value_error_in_artifact_builder_returns_static_detail(self):
        with patch.object(_module, "_build_study_schedule", side_effect=ValueError("bad value")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_unexpected_error_response(result)

    def test_runtime_error_in_artifact_builder_returns_static_detail(self):
        with patch.object(_module, "_build_watchdog_baseline", side_effect=RuntimeError("runtime fail")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_unexpected_error_response(result)

    def test_attribute_error_returns_static_detail(self):
        with patch.object(_module, "_build_operator_readiness", side_effect=AttributeError("no attr")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_unexpected_error_response(result)

    def test_exception_message_not_leaked_in_response(self):
        """Raw exception message must NOT appear in the error detail (no information disclosure)."""
        sentinel = "SENSITIVE_INTERNAL_DETAIL_12345"
        with patch.object(_module, "_build_operator_readiness", side_effect=RuntimeError(sentinel)):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertNotIn(sentinel, result.get("error", ""))
        self.assertNotIn(sentinel, json.dumps(result))

    def test_unexpected_error_detail_is_static_string(self):
        """Error detail must be exactly the required static string, not a dynamic value."""
        with patch.object(_module, "_build_operator_readiness", side_effect=Exception("dynamic msg")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertEqual(result["error"], _STATIC_UNEXPECTED_ERROR)


# ---------------------------------------------------------------------------
# Group 3: Happy path / valid input
# ---------------------------------------------------------------------------
class TestHappyPath(unittest.TestCase):
    """Valid inputs must produce ok=True with all four artifact types."""

    def test_empty_input_returns_ok_true(self):
        result = _run_main_with_input("")
        self.assertTrue(result["ok"])

    def test_empty_input_produces_four_artifacts(self):
        result = _run_main_with_input("")
        self.assertEqual(len(result["artifacts"]), 4)

    def test_valid_input_returns_ok_true(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertTrue(result["ok"])

    def test_valid_input_produces_four_artifacts(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertEqual(len(result["artifacts"]), 4)

    def test_artifact_types_are_present(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        types = {a["artifactType"] for a in result["artifacts"]}
        self.assertEqual(
            types,
            {
                "operator-readiness",
                "knowledge-index",
                "study-schedule",
                "watchdog-baseline",
            },
        )

    def test_each_artifact_has_required_fields(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        required = {"artifactType", "version", "generatedAt", "source", "reviewRequired", "data"}
        for artifact in result["artifacts"]:
            for field in required:
                self.assertIn(field, artifact, f"Missing field '{field}' in {artifact['artifactType']}")

    def test_source_is_office_ml_pipeline(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        for artifact in result["artifacts"]:
            self.assertEqual(artifact["source"], "office-ml-pipeline")

    def test_version_is_semver(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        for artifact in result["artifacts"]:
            parts = artifact["version"].split(".")
            self.assertEqual(len(parts), 3, f"Bad version in {artifact['artifactType']}")

    def test_whitespace_only_input_returns_ok_true(self):
        result = _run_main_with_input("   \n  ")
        self.assertTrue(result["ok"])

    def test_empty_json_object_returns_ok_true(self):
        result = _run_main_with_input("{}")
        self.assertTrue(result["ok"])


# ---------------------------------------------------------------------------
# Group 4: Happy path – response format compliance
# ---------------------------------------------------------------------------
class TestHappyPathResponseFormat(unittest.TestCase):
    """Verifies strict compliance with {"ok": true, "artifacts": [...]} format."""

    def _get_artifact(self, result: dict, artifact_type: str) -> dict:
        for a in result["artifacts"]:
            if a["artifactType"] == artifact_type:
                return a
        self.fail(f"Artifact type '{artifact_type}' not found in response")

    def test_response_has_ok_key(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertIn("ok", result)

    def test_response_has_artifacts_key(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertIn("artifacts", result)

    def test_artifacts_is_a_list(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertIsInstance(result["artifacts"], list)

    def test_response_has_generated_at_key(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertIn("generatedAt", result)

    def test_generated_at_is_iso_timestamp(self):
        """Top-level generatedAt must be a valid ISO 8601 UTC timestamp."""
        from datetime import datetime
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        ts = result["generatedAt"]
        parsed = datetime.fromisoformat(ts)
        self.assertIsNotNone(parsed)

    def test_each_artifact_generated_at_is_iso_timestamp(self):
        from datetime import datetime
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        for artifact in result["artifacts"]:
            ts = artifact["generatedAt"]
            parsed = datetime.fromisoformat(ts)
            self.assertIsNotNone(parsed, f"Bad generatedAt in {artifact['artifactType']}")

    def test_operator_readiness_review_required_is_true(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result, "operator-readiness")
        self.assertTrue(artifact["reviewRequired"])

    def test_knowledge_index_review_required_is_false(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result, "knowledge-index")
        self.assertFalse(artifact["reviewRequired"])

    def test_study_schedule_review_required_is_true(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result, "study-schedule")
        self.assertTrue(artifact["reviewRequired"])

    def test_watchdog_baseline_review_required_is_false(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result, "watchdog-baseline")
        self.assertFalse(artifact["reviewRequired"])

    def test_version_parts_are_numeric(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        for artifact in result["artifacts"]:
            parts = artifact["version"].split(".")
            for part in parts:
                self.assertTrue(
                    part.isdigit(),
                    f"Non-numeric version segment '{part}' in {artifact['artifactType']}",
                )


# ---------------------------------------------------------------------------
# Group 5: Happy path – operator-readiness artifact data
# ---------------------------------------------------------------------------
class TestHappyPathOperatorReadiness(unittest.TestCase):
    """Verifies the data content of the operator-readiness artifact."""

    def _get_artifact(self, result: dict) -> dict:
        for a in result["artifacts"]:
            if a["artifactType"] == "operator-readiness":
                return a
        self.fail("operator-readiness artifact not found")

    def test_data_has_overall_readiness(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("overallReadiness", artifact["data"])

    def test_data_has_skill_signals(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("skillSignals", artifact["data"])

    def test_data_has_active_anomalies(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("activeAnomalies", artifact["data"])

    def test_data_has_recommendation(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("recommendation", artifact["data"])

    def test_high_readiness_no_anomalies_recommendation_is_ready(self):
        """overallReadiness >= 0.75 and no anomalies → recommendation = 'ready'."""
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["recommendation"], "ready")

    def test_mid_readiness_recommendation_is_developing(self):
        """overallReadiness >= 0.5 but < 0.75 → recommendation = 'developing'."""
        result = _run_main_with_input(_MID_READINESS_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["recommendation"], "developing")

    def test_low_readiness_recommendation_is_needs_study(self):
        """overallReadiness < 0.5 → recommendation = 'needs-study'."""
        result = _run_main_with_input(_LOW_READINESS_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["recommendation"], "needs-study")

    def test_skill_signals_populated_from_readiness_breakdown(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        signals = artifact["data"]["skillSignals"]
        self.assertEqual(len(signals), 2)
        topics = {s["topic"] for s in signals}
        self.assertEqual(topics, {"electrical-safety", "circuit-theory"})

    def test_skill_signal_has_required_fields(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        required = {"topic", "readiness", "confidence", "trend", "improving", "mastered", "estimatedDaysToMastery"}
        for signal in artifact["data"]["skillSignals"]:
            for field in required:
                self.assertIn(field, signal, f"Missing field '{field}' in skill signal")

    def test_mastered_topic_reflected_in_skill_signal(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        esignal = next(
            s for s in artifact["data"]["skillSignals"] if s["topic"] == "electrical-safety"
        )
        self.assertTrue(esignal["mastered"])

    def test_active_anomalies_count_reflects_forecast_anomalies(self):
        result = _run_main_with_input(_LOW_READINESS_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["activeAnomalies"], 1)

    def test_anomaly_prevents_ready_recommendation(self):
        """High readiness but active anomalies must not produce 'ready' recommendation."""
        result = _run_main_with_input(_LOW_READINESS_INPUT)
        artifact = self._get_artifact(result)
        self.assertNotEqual(artifact["data"]["recommendation"], "ready")


# ---------------------------------------------------------------------------
# Group 6: Happy path – knowledge-index artifact data
# ---------------------------------------------------------------------------
class TestHappyPathKnowledgeIndex(unittest.TestCase):
    """Verifies the data content of the knowledge-index artifact."""

    def _get_artifact(self, result: dict) -> dict:
        for a in result["artifacts"]:
            if a["artifactType"] == "knowledge-index":
                return a
        self.fail("knowledge-index artifact not found")

    def test_data_has_total_documents(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("totalDocuments", artifact["data"])

    def test_data_has_indexed_documents(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("indexedDocuments", artifact["data"])

    def test_data_has_document_clusters(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("documentClusters", artifact["data"])

    def test_data_has_embedding_engine(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("embeddingEngine", artifact["data"])

    def test_empty_embeddings_total_documents_is_zero(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["totalDocuments"], 0)

    def test_rich_input_total_documents_matches_embeddings(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["totalDocuments"], 2)

    def test_rich_input_indexed_documents_have_required_fields(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        required = {"documentId", "title", "dimensions", "indexed"}
        for doc in artifact["data"]["indexedDocuments"]:
            for field in required:
                self.assertIn(field, doc, f"Missing field '{field}' in indexed document")

    def test_rich_input_document_clusters_formed_from_high_similarity(self):
        """Pairs with similarity > 0.5 must produce a cluster entry.

        The rich input fixture contains one pair with similarity=0.72, which is
        above the 0.5 threshold used by _build_knowledge_index, so exactly one
        cluster is expected.
        """
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        clusters = artifact["data"]["documentClusters"]
        self.assertEqual(len(clusters), 1)
        # Verify the cluster contains both documents from the high-similarity pair
        cluster_docs = set(clusters[0]["documents"])
        self.assertEqual(cluster_docs, {"doc-001", "doc-002"})
        self.assertGreater(clusters[0]["similarity"], 0.5)

    def test_embedding_engine_reflects_input_engine(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["embeddingEngine"], "test-embed-engine")


# ---------------------------------------------------------------------------
# Group 7: Happy path – study-schedule artifact data
# ---------------------------------------------------------------------------
class TestHappyPathStudySchedule(unittest.TestCase):
    """Verifies the data content of the study-schedule artifact."""

    def _get_artifact(self, result: dict) -> dict:
        for a in result["artifacts"]:
            if a["artifactType"] == "study-schedule":
                return a
        self.fail("study-schedule artifact not found")

    def test_data_has_schedule(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("schedule", artifact["data"])

    def test_data_has_total_topics(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("totalTopics", artifact["data"])

    def test_data_has_plateau_count(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("plateauCount", artifact["data"])

    def test_data_has_analytics_engine(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("analyticsEngine", artifact["data"])

    def test_data_has_forecast_engine(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("forecastEngine", artifact["data"])

    def test_empty_schedule_total_topics_is_zero(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["totalTopics"], 0)

    def test_rich_input_schedule_has_correct_topic_count(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["totalTopics"], 2)

    def test_plateau_topic_detected_in_schedule(self):
        """A topic listed in plateaus must have plateauDetected=True in the schedule."""
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        circuit = next(
            e for e in artifact["data"]["schedule"] if e["topic"] == "circuit-theory"
        )
        self.assertTrue(circuit["plateauDetected"])

    def test_non_plateau_topic_detected_false_in_schedule(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        esafety = next(
            e for e in artifact["data"]["schedule"] if e["topic"] == "electrical-safety"
        )
        self.assertFalse(esafety["plateauDetected"])

    def test_plateau_recommendation_included_in_enriched_entry(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        circuit = next(
            e for e in artifact["data"]["schedule"] if e["topic"] == "circuit-theory"
        )
        self.assertIn("plateauRecommendation", circuit)
        self.assertEqual(circuit["plateauRecommendation"], "try spaced repetition")

    def test_plateau_count_reflects_forecast_plateaus(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(artifact["data"]["plateauCount"], 1)


# ---------------------------------------------------------------------------
# Group 8: Happy path – watchdog-baseline artifact data
# ---------------------------------------------------------------------------
class TestHappyPathWatchdogBaseline(unittest.TestCase):
    """Verifies the data content of the watchdog-baseline artifact."""

    def _get_artifact(self, result: dict) -> dict:
        for a in result["artifacts"]:
            if a["artifactType"] == "watchdog-baseline":
                return a
        self.fail("watchdog-baseline artifact not found")

    def test_data_has_baseline_metrics(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("baselineMetrics", artifact["data"])

    def test_data_has_active_anomalies(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("activeAnomalies", artifact["data"])

    def test_data_has_topic_clusters(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("topicClusters", artifact["data"])

    def test_data_has_monitoring_enabled(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertIn("monitoringEnabled", artifact["data"])

    def test_empty_forecasts_monitoring_disabled(self):
        result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertFalse(artifact["data"]["monitoringEnabled"])

    def test_rich_input_monitoring_enabled(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertTrue(artifact["data"]["monitoringEnabled"])

    def test_rich_input_baseline_metrics_count_matches_forecasts(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(len(artifact["data"]["baselineMetrics"]), 2)

    def test_baseline_metric_has_required_fields(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        required = {"topic", "baselineAccuracy", "expectedRange", "trend", "dataPoints"}
        for metric in artifact["data"]["baselineMetrics"]:
            for field in required:
                self.assertIn(field, metric, f"Missing field '{field}' in baseline metric")

    def test_expected_range_has_low_and_high(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        for metric in artifact["data"]["baselineMetrics"]:
            self.assertIn("low", metric["expectedRange"])
            self.assertIn("high", metric["expectedRange"])

    def test_expected_range_low_is_non_negative(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        for metric in artifact["data"]["baselineMetrics"]:
            self.assertGreaterEqual(metric["expectedRange"]["low"], 0.0)

    def test_expected_range_high_is_at_most_one(self):
        result = _run_main_with_input(_RICH_VALID_INPUT)
        artifact = self._get_artifact(result)
        for metric in artifact["data"]["baselineMetrics"]:
            self.assertLessEqual(metric["expectedRange"]["high"], 1.0)

    def test_active_anomalies_passed_through_from_forecast(self):
        result = _run_main_with_input(_LOW_READINESS_INPUT)
        artifact = self._get_artifact(result)
        self.assertEqual(len(artifact["data"]["activeAnomalies"]), 1)


if __name__ == "__main__":
    unittest.main()
