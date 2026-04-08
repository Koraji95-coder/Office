"""
Integration tests for exception handling in ml_retrain_analytics.py.

Verifies that all exception paths produce compliant static error detail strings:
  - JSONDecodeError → {"ok": false, "error": "Invalid JSON input."}
  - Training-step exceptions → reason: "An unexpected error occurred. See server logs for details."
  - Happy path → {"ok": true, "readiness_predictor": ..., "operator_classifier": ..., "topic_clusters": ...}
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

import ml_retrain_analytics as _module  # noqa: E402


# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
_STATIC_TRAIN_ERROR = (
    "An unexpected error occurred. See server logs for details."
)
_STATIC_JSON_ERROR = "Invalid JSON input."

_MINIMAL_VALID_INPUT = json.dumps(
    {
        "trainingAttempts": [
            {
                "completedAt": "2024-01-01T00:00:00Z",
                "questions": [
                    {"topic": "algebra", "correct": True},
                    {"topic": "geometry", "correct": False},
                ],
            }
        ],
        "operatorDecisions": [
            {"status": "accepted", "decidedAt": "2024-01-01T00:00:00Z"},
        ],
    }
)


# ---------------------------------------------------------------------------
# Stub metrics returned by training helpers when mocked for happy-path tests
# ---------------------------------------------------------------------------
_STUB_CLUSTERING = {"n_clusters": 2, "silhouette_score": 0.5, "n_topics": 2, "saved": False}
_STUB_READINESS = {"cv_r2": 0.5, "cv_r2_std": 0.1, "training_samples": 10, "saved": False}
_STUB_OPERATOR = {"cv_f1": 0.6, "cv_f1_std": 0.1, "training_samples": 5, "classes": ["accepted"], "saved": False}


# ---------------------------------------------------------------------------
# Helper
# ---------------------------------------------------------------------------
def _run_main_with_input(stdin_text: str, sklearn_available: bool = True) -> dict:
    """Run main() with the given stdin text and return parsed stdout JSON."""
    with patch.object(_module, "_try_import_sklearn", return_value=sklearn_available), \
         patch.object(_module, "_export_metrics"):
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
        result = _run_main_with_input('{"trainingAttempts": [')
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
# Group 2: Training-step exception paths
# ---------------------------------------------------------------------------
class TestTrainingStepExceptionPath(unittest.TestCase):
    """Training-step exceptions must store a static 'reason', not str(exc)."""

    def _assert_static_reason(self, metrics_key: str, result: dict) -> None:
        section = result.get(metrics_key, {})
        self.assertFalse(section.get("saved"), f"{metrics_key} should have saved=False")
        self.assertEqual(
            section.get("reason"),
            _STATIC_TRAIN_ERROR,
            f"{metrics_key} should have static reason string",
        )

    # --- clustering (topic_clusters) ---

    def test_clustering_exception_ok_still_true(self):
        with patch.object(_module, "_train_topic_clustering", side_effect=RuntimeError("internal detail")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertTrue(result["ok"])

    def test_clustering_exception_reason_is_static(self):
        with patch.object(_module, "_train_topic_clustering", side_effect=RuntimeError("internal detail")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_static_reason("topic_clusters", result)

    def test_clustering_exception_does_not_leak_message(self):
        sentinel = "SENSITIVE_CLUSTER_DETAIL_99999"
        with patch.object(_module, "_train_topic_clustering", side_effect=RuntimeError(sentinel)):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertNotIn(sentinel, result["topic_clusters"].get("reason", ""))
        self.assertNotIn(sentinel, json.dumps(result))

    def test_clustering_key_error_reason_is_static(self):
        with patch.object(_module, "_train_topic_clustering", side_effect=KeyError("missing_key")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_static_reason("topic_clusters", result)

    def test_clustering_value_error_reason_is_static(self):
        with patch.object(_module, "_train_topic_clustering", side_effect=ValueError("bad value")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_static_reason("topic_clusters", result)

    # --- readiness predictor ---

    def test_readiness_exception_ok_still_true(self):
        with patch.object(_module, "_train_readiness_predictor", side_effect=RuntimeError("internal detail")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertTrue(result["ok"])

    def test_readiness_exception_reason_is_static(self):
        with patch.object(_module, "_train_readiness_predictor", side_effect=RuntimeError("internal detail")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_static_reason("readiness_predictor", result)

    def test_readiness_exception_does_not_leak_message(self):
        sentinel = "SENSITIVE_READINESS_DETAIL_88888"
        with patch.object(_module, "_train_readiness_predictor", side_effect=RuntimeError(sentinel)):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertNotIn(sentinel, result["readiness_predictor"].get("reason", ""))
        self.assertNotIn(sentinel, json.dumps(result))

    def test_readiness_type_error_reason_is_static(self):
        with patch.object(_module, "_train_readiness_predictor", side_effect=TypeError("bad type")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_static_reason("readiness_predictor", result)

    def test_readiness_attribute_error_reason_is_static(self):
        with patch.object(_module, "_train_readiness_predictor", side_effect=AttributeError("no attr")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_static_reason("readiness_predictor", result)

    # --- operator classifier ---

    def test_operator_exception_ok_still_true(self):
        with patch.object(_module, "_train_operator_classifier", side_effect=RuntimeError("internal detail")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertTrue(result["ok"])

    def test_operator_exception_reason_is_static(self):
        with patch.object(_module, "_train_operator_classifier", side_effect=RuntimeError("internal detail")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_static_reason("operator_classifier", result)

    def test_operator_exception_does_not_leak_message(self):
        sentinel = "SENSITIVE_OPERATOR_DETAIL_77777"
        with patch.object(_module, "_train_operator_classifier", side_effect=RuntimeError(sentinel)):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertNotIn(sentinel, result["operator_classifier"].get("reason", ""))
        self.assertNotIn(sentinel, json.dumps(result))

    def test_operator_key_error_reason_is_static(self):
        with patch.object(_module, "_train_operator_classifier", side_effect=KeyError("status")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_static_reason("operator_classifier", result)

    def test_operator_value_error_reason_is_static(self):
        with patch.object(_module, "_train_operator_classifier", side_effect=ValueError("bad value")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self._assert_static_reason("operator_classifier", result)

    def test_all_three_steps_fail_all_reasons_are_static(self):
        """All three training steps failing simultaneously must all return static reasons."""
        with patch.object(_module, "_train_topic_clustering", side_effect=RuntimeError("err1")), \
             patch.object(_module, "_train_readiness_predictor", side_effect=RuntimeError("err2")), \
             patch.object(_module, "_train_operator_classifier", side_effect=RuntimeError("err3")):
            result = _run_main_with_input(_MINIMAL_VALID_INPUT)
        self.assertTrue(result["ok"])
        self._assert_static_reason("topic_clusters", result)
        self._assert_static_reason("readiness_predictor", result)
        self._assert_static_reason("operator_classifier", result)


# ---------------------------------------------------------------------------
# Group 3: Happy path / valid input
# ---------------------------------------------------------------------------
class TestHappyPath(unittest.TestCase):
    """Valid inputs must produce ok=True with all three model metric sections."""

    def _run_with_stubs(self, stdin_text: str) -> dict:
        with patch.object(_module, "_train_topic_clustering", return_value=_STUB_CLUSTERING), \
             patch.object(_module, "_train_readiness_predictor", return_value=_STUB_READINESS), \
             patch.object(_module, "_train_operator_classifier", return_value=_STUB_OPERATOR):
            return _run_main_with_input(stdin_text)

    def test_empty_input_returns_ok_true(self):
        result = self._run_with_stubs("")
        self.assertTrue(result["ok"])

    def test_empty_input_has_all_three_sections(self):
        result = self._run_with_stubs("")
        self.assertIn("readiness_predictor", result)
        self.assertIn("operator_classifier", result)
        self.assertIn("topic_clusters", result)

    def test_valid_input_returns_ok_true(self):
        result = self._run_with_stubs(_MINIMAL_VALID_INPUT)
        self.assertTrue(result["ok"])

    def test_valid_input_has_all_three_sections(self):
        result = self._run_with_stubs(_MINIMAL_VALID_INPUT)
        self.assertIn("readiness_predictor", result)
        self.assertIn("operator_classifier", result)
        self.assertIn("topic_clusters", result)

    def test_result_has_retrained_at_field(self):
        result = self._run_with_stubs(_MINIMAL_VALID_INPUT)
        self.assertIn("retrained_at", result)

    def test_whitespace_only_input_returns_ok_true(self):
        result = self._run_with_stubs("   \n  ")
        self.assertTrue(result["ok"])

    def test_empty_json_object_returns_ok_true(self):
        result = self._run_with_stubs("{}")
        self.assertTrue(result["ok"])


if __name__ == "__main__":
    unittest.main()
