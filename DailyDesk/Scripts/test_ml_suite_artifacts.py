"""
Integration tests for error handling in ml_suite_artifacts.py.

Verifies that all exception paths produce compliant static error detail strings:
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
# Group 4: Real KeyError integration tests (data-driven, no mocks)
# ---------------------------------------------------------------------------
class TestKeyErrorIntegration(unittest.TestCase):
    """Passing malformed input data that causes real KeyErrors in builder functions.

    Verifies that each direct key access in the builder functions (``entry["topic"]``,
    ``embedding["documentId"]``, etc.) is covered by the outer exception handler and
    returns the static error string rather than leaking internal detail.
    """

    def _assert_static_error(self, result: dict) -> None:
        self.assertFalse(result["ok"])
        self.assertEqual(result["error"], _STATIC_UNEXPECTED_ERROR)

    # -- _build_operator_readiness --

    def test_operator_readiness_missing_topic_returns_static_error(self):
        """readinessBreakdown entry without 'topic' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {
                    "readinessBreakdown": [{"readiness": 0.8}],  # "topic" missing
                    "adaptiveSchedule": [],
                    "topicClusters": [],
                },
                "embeddings": {},
                "forecast": {"masteryEstimates": [], "anomalies": [], "plateaus": [], "forecasts": []},
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    def test_operator_readiness_missing_readiness_returns_static_error(self):
        """readinessBreakdown entry without 'readiness' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {
                    "readinessBreakdown": [{"topic": "Circuits"}],  # "readiness" missing
                    "adaptiveSchedule": [],
                    "topicClusters": [],
                },
                "embeddings": {},
                "forecast": {"masteryEstimates": [], "anomalies": [], "plateaus": [], "forecasts": []},
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    # -- _build_knowledge_index --

    def test_knowledge_index_missing_document_id_returns_static_error(self):
        """Embedding entry without 'documentId' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {},
                "embeddings": {
                    "embeddings": [{"title": "Doc A", "dimensions": 128}],  # "documentId" missing
                    "similarities": [],
                },
                "forecast": {},
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    def test_knowledge_index_missing_title_returns_static_error(self):
        """Embedding entry without 'title' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {},
                "embeddings": {
                    "embeddings": [{"documentId": "doc-1", "dimensions": 128}],  # "title" missing
                    "similarities": [],
                },
                "forecast": {},
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    def test_knowledge_index_missing_dimensions_returns_static_error(self):
        """Embedding entry without 'dimensions' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {},
                "embeddings": {
                    "embeddings": [{"documentId": "doc-1", "title": "Doc 1"}],  # "dimensions" missing
                    "similarities": [],
                },
                "forecast": {},
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    def test_knowledge_index_missing_document_a_in_similarity_returns_static_error(self):
        """Similarity pair without 'documentA' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {},
                "embeddings": {
                    "embeddings": [],
                    "similarities": [{"documentB": "doc-2", "similarity": 0.9}],  # "documentA" missing
                },
                "forecast": {},
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    def test_knowledge_index_missing_document_b_in_similarity_returns_static_error(self):
        """Similarity pair without 'documentB' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {},
                "embeddings": {
                    "embeddings": [],
                    "similarities": [{"documentA": "doc-1", "similarity": 0.9}],  # "documentB" missing
                },
                "forecast": {},
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    # -- _build_study_schedule --

    def test_study_schedule_missing_topic_returns_static_error(self):
        """adaptiveSchedule entry without 'topic' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {
                    "readinessBreakdown": [],
                    "adaptiveSchedule": [{"sessions": 3}],  # "topic" missing
                    "topicClusters": [],
                },
                "embeddings": {},
                "forecast": {"masteryEstimates": [], "anomalies": [], "plateaus": [], "forecasts": []},
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    # -- _build_watchdog_baseline --

    def test_watchdog_baseline_missing_topic_returns_static_error(self):
        """Forecast entry without 'topic' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {"readinessBreakdown": [], "adaptiveSchedule": [], "topicClusters": []},
                "embeddings": {},
                "forecast": {
                    "masteryEstimates": [],
                    "anomalies": [],
                    "plateaus": [],
                    "forecasts": [
                        {"currentAccuracy": 0.8, "trend": "up", "dataPoints": 10}  # "topic" missing
                    ],
                },
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    def test_watchdog_baseline_missing_current_accuracy_returns_static_error(self):
        """Forecast entry without 'currentAccuracy' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {"readinessBreakdown": [], "adaptiveSchedule": [], "topicClusters": []},
                "embeddings": {},
                "forecast": {
                    "masteryEstimates": [],
                    "anomalies": [],
                    "plateaus": [],
                    "forecasts": [
                        {"topic": "Circuits", "trend": "up", "dataPoints": 10}  # "currentAccuracy" missing
                    ],
                },
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    def test_watchdog_baseline_missing_trend_returns_static_error(self):
        """Forecast entry without 'trend' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {"readinessBreakdown": [], "adaptiveSchedule": [], "topicClusters": []},
                "embeddings": {},
                "forecast": {
                    "masteryEstimates": [],
                    "anomalies": [],
                    "plateaus": [],
                    "forecasts": [
                        {"topic": "Circuits", "currentAccuracy": 0.8, "dataPoints": 10}  # "trend" missing
                    ],
                },
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    def test_watchdog_baseline_missing_data_points_returns_static_error(self):
        """Forecast entry without 'dataPoints' key raises KeyError."""
        payload = json.dumps(
            {
                "analytics": {"readinessBreakdown": [], "adaptiveSchedule": [], "topicClusters": []},
                "embeddings": {},
                "forecast": {
                    "masteryEstimates": [],
                    "anomalies": [],
                    "plateaus": [],
                    "forecasts": [
                        {"topic": "Circuits", "currentAccuracy": 0.8, "trend": "up"}  # "dataPoints" missing
                    ],
                },
            }
        )
        self._assert_static_error(_run_main_with_input(payload))

    # -- No information disclosure --

    def test_key_error_does_not_leak_missing_key_name(self):
        """The missing key name must NOT appear in the error response."""
        payload = json.dumps(
            {
                "analytics": {
                    "readinessBreakdown": [{"readiness": 0.8}],  # triggers KeyError("topic")
                    "adaptiveSchedule": [],
                    "topicClusters": [],
                },
                "embeddings": {},
                "forecast": {"masteryEstimates": [], "anomalies": [], "plateaus": [], "forecasts": []},
            }
        )
        result = _run_main_with_input(payload)
        self.assertNotIn("topic", result.get("error", ""))
        self.assertNotIn("KeyError", result.get("error", ""))


if __name__ == "__main__":
    unittest.main()
