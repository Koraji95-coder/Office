"""
test_validate_schema.py — Unit tests for validate_schema.py

Covers:
  - Happy path: a fully valid minimal sample passes validation.
  - Missing required fields: each required field is individually omitted.
  - Type errors: wrong Python types for typed fields.
  - Range violations: integers and numbers outside schema-defined min/max.
  - Enum violations: verdict set to an unknown string.
  - Nullable oneOf fields: null values are accepted for optional cluster/forecast fields.
  - Array item type errors: non-string items inside the 'files' and 'concerns' arrays.
  - CLI JSON-parse error path: malformed stdin produces a non-zero exit code.

Run with:
    python -m unittest scripts/scoring/test_validate_schema.py  (from repo root)
  or:
    python -m unittest test_validate_schema                     (from scripts/scoring/)
"""

import json
import subprocess
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

# ---------------------------------------------------------------------------
# Make sure the module under test is importable regardless of working dir
# ---------------------------------------------------------------------------
_THIS_DIR = Path(__file__).parent
sys.path.insert(0, str(_THIS_DIR))

import validate_schema as _module  # noqa: E402
from validate_schema import validate_sample  # noqa: E402


# ---------------------------------------------------------------------------
# Shared fixture helpers
# ---------------------------------------------------------------------------

_REQUIRED_FIELDS = (
    "pr_number",
    "repo",
    "title",
    "author",
    "timestamp",
    "additions",
    "deletions",
    "file_count",
    "files",
    "score",
    "verdict",
    "decision",
    "model",
    "json_parsed",
)


def _minimal_valid() -> dict:
    """Return a minimal dict that satisfies every required schema constraint."""
    return {
        "pr_number": 42,
        "repo": "Office",
        "title": "Add feature X",
        "author": "dev",
        "timestamp": "2024-01-15T10:00:00Z",
        "additions": 10,
        "deletions": 2,
        "file_count": 3,
        "files": ["src/a.cs", "src/b.cs", "tests/c.cs"],
        "score": 7,
        "verdict": "APPROVE",
        "decision": "auto-merge",
        "model": "qwen3:14b",
        "json_parsed": True,
    }


# ---------------------------------------------------------------------------
# Group 1: Happy path
# ---------------------------------------------------------------------------

class TestValidSample(unittest.TestCase):
    """A fully conformant sample must pass with no errors."""

    def test_minimal_valid_sample_is_ok(self):
        ok, errors = validate_sample(_minimal_valid())
        self.assertTrue(ok)
        self.assertEqual(errors, [])

    def test_returns_two_element_tuple(self):
        result = validate_sample(_minimal_valid())
        self.assertIsInstance(result, tuple)
        self.assertEqual(len(result), 2)

    def test_first_element_is_bool(self):
        ok, _ = validate_sample(_minimal_valid())
        self.assertIsInstance(ok, bool)

    def test_second_element_is_list(self):
        _, errors = validate_sample(_minimal_valid())
        self.assertIsInstance(errors, list)

    def test_optional_nullable_fields_accepted_as_null(self):
        sample = _minimal_valid()
        sample["cluster_id"] = None
        sample["cluster_label"] = None
        sample["cluster_confidence"] = None
        sample["forecast_confidence"] = None
        sample["plateau_detected"] = None
        sample["embedding_similarity_scores"] = None
        ok, errors = validate_sample(sample)
        self.assertTrue(ok, errors)

    def test_optional_nullable_fields_accepted_with_values(self):
        sample = _minimal_valid()
        sample["cluster_id"] = 3
        sample["cluster_label"] = "bugfix"
        sample["cluster_confidence"] = 0.85
        sample["forecast_confidence"] = 0.9
        sample["plateau_detected"] = False
        sample["embedding_similarity_scores"] = [0.1, 0.9, 0.5]
        ok, errors = validate_sample(sample)
        self.assertTrue(ok, errors)

    def test_score_boundary_1_is_valid(self):
        sample = _minimal_valid()
        sample["score"] = 1
        ok, errors = validate_sample(sample)
        self.assertTrue(ok, errors)

    def test_score_boundary_10_is_valid(self):
        sample = _minimal_valid()
        sample["score"] = 10
        ok, errors = validate_sample(sample)
        self.assertTrue(ok, errors)

    def test_all_verdict_enum_values_are_valid(self):
        for verdict in ("APPROVE", "REQUEST_CHANGES", "NEEDS_DISCUSSION", "UNKNOWN"):
            sample = _minimal_valid()
            sample["verdict"] = verdict
            ok, errors = validate_sample(sample)
            self.assertTrue(ok, f"verdict '{verdict}' should be valid, errors: {errors}")

    def test_verdict_enum_values_match_schema(self):
        """Ensure the expected enum values stay in sync with the actual schema definition."""
        schema = _module._load_schema()
        schema_enum = schema["properties"]["verdict"]["enum"]
        expected = ["APPROVE", "REQUEST_CHANGES", "NEEDS_DISCUSSION", "UNKNOWN"]
        self.assertEqual(sorted(schema_enum), sorted(expected))

    def test_empty_files_array_is_valid(self):
        sample = _minimal_valid()
        sample["files"] = []
        ok, errors = validate_sample(sample)
        self.assertTrue(ok, errors)

    def test_concerns_array_accepted(self):
        sample = _minimal_valid()
        sample["concerns"] = ["Missing tests", "Large diff"]
        ok, errors = validate_sample(sample)
        self.assertTrue(ok, errors)

    def test_additional_properties_are_accepted(self):
        sample = _minimal_valid()
        sample["extra_field"] = "anything"
        ok, errors = validate_sample(sample)
        self.assertTrue(ok, errors)


# ---------------------------------------------------------------------------
# Group 2: Missing required fields
# ---------------------------------------------------------------------------

class TestMissingRequiredFields(unittest.TestCase):
    """Omitting any required field must produce exactly one validation error."""

    def _assert_required_error(self, field: str):
        sample = _minimal_valid()
        del sample[field]
        ok, errors = validate_sample(sample)
        self.assertFalse(ok, f"Expected failure when '{field}' is missing")
        self.assertTrue(
            any(field in e for e in errors),
            f"Expected an error mentioning '{field}', got: {errors}",
        )

    def test_missing_pr_number(self):
        self._assert_required_error("pr_number")

    def test_missing_repo(self):
        self._assert_required_error("repo")

    def test_missing_title(self):
        self._assert_required_error("title")

    def test_missing_author(self):
        self._assert_required_error("author")

    def test_missing_timestamp(self):
        self._assert_required_error("timestamp")

    def test_missing_additions(self):
        self._assert_required_error("additions")

    def test_missing_deletions(self):
        self._assert_required_error("deletions")

    def test_missing_file_count(self):
        self._assert_required_error("file_count")

    def test_missing_files(self):
        self._assert_required_error("files")

    def test_missing_score(self):
        self._assert_required_error("score")

    def test_missing_verdict(self):
        self._assert_required_error("verdict")

    def test_missing_decision(self):
        self._assert_required_error("decision")

    def test_missing_model(self):
        self._assert_required_error("model")

    def test_missing_json_parsed(self):
        self._assert_required_error("json_parsed")

    def test_multiple_missing_fields_all_reported(self):
        sample = _minimal_valid()
        del sample["pr_number"]
        del sample["repo"]
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreaterEqual(len(errors), 2)


# ---------------------------------------------------------------------------
# Group 3: Type violations
# ---------------------------------------------------------------------------

class TestTypeViolations(unittest.TestCase):
    """Fields with incorrect Python types must fail validation."""

    def _assert_type_error(self, field: str, bad_value):
        sample = _minimal_valid()
        sample[field] = bad_value
        ok, errors = validate_sample(sample)
        self.assertFalse(ok, f"Expected failure for '{field}' = {bad_value!r}")
        self.assertTrue(len(errors) > 0, f"Expected at least one error, got none")

    def test_pr_number_as_string_fails(self):
        self._assert_type_error("pr_number", "42")

    def test_score_as_string_fails(self):
        self._assert_type_error("score", "7")

    def test_score_as_float_integral_is_accepted(self):
        # JSON Schema Draft 7 treats 7.0 as a valid integer value
        sample = _minimal_valid()
        sample["score"] = 7.0
        ok, _ = validate_sample(sample)
        self.assertTrue(ok)

    def test_additions_as_string_fails(self):
        self._assert_type_error("additions", "10")

    def test_json_parsed_as_string_fails(self):
        self._assert_type_error("json_parsed", "true")

    def test_json_parsed_as_integer_fails(self):
        self._assert_type_error("json_parsed", 1)

    def test_files_as_string_fails(self):
        self._assert_type_error("files", "src/a.cs")

    def test_files_as_dict_fails(self):
        self._assert_type_error("files", {"src/a.cs": True})

    def test_verdict_as_integer_fails(self):
        self._assert_type_error("verdict", 1)


# ---------------------------------------------------------------------------
# Group 4: Range violations
# ---------------------------------------------------------------------------

class TestRangeViolations(unittest.TestCase):
    """Fields with out-of-range values must fail validation."""

    def test_score_zero_fails(self):
        sample = _minimal_valid()
        sample["score"] = 0
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)

    def test_score_eleven_fails(self):
        sample = _minimal_valid()
        sample["score"] = 11
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)

    def test_additions_negative_fails(self):
        sample = _minimal_valid()
        sample["additions"] = -1
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)

    def test_deletions_negative_fails(self):
        sample = _minimal_valid()
        sample["deletions"] = -5
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)

    def test_file_count_negative_fails(self):
        sample = _minimal_valid()
        sample["file_count"] = -1
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)

    def test_additions_zero_is_valid(self):
        sample = _minimal_valid()
        sample["additions"] = 0
        ok, errors = validate_sample(sample)
        self.assertTrue(ok, errors)

    def test_cluster_confidence_above_one_fails(self):
        sample = _minimal_valid()
        sample["cluster_confidence"] = 1.5
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)

    def test_cluster_confidence_negative_fails(self):
        sample = _minimal_valid()
        sample["cluster_confidence"] = -0.1
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)

    def test_forecast_confidence_above_one_fails(self):
        sample = _minimal_valid()
        sample["forecast_confidence"] = 2.0
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)


# ---------------------------------------------------------------------------
# Group 5: Enum violations
# ---------------------------------------------------------------------------

class TestEnumViolations(unittest.TestCase):
    """verdict must be one of the four allowed enum values."""

    def _assert_verdict_invalid(self, verdict_value: str):
        sample = _minimal_valid()
        sample["verdict"] = verdict_value
        ok, errors = validate_sample(sample)
        self.assertFalse(ok, f"Expected failure for verdict '{verdict_value}'")
        self.assertGreater(len(errors), 0, f"Expected at least one error, got none")

    def test_verdict_lowercase_approve_fails(self):
        self._assert_verdict_invalid("approve")

    def test_verdict_arbitrary_string_fails(self):
        self._assert_verdict_invalid("MERGE")

    def test_verdict_empty_string_fails(self):
        self._assert_verdict_invalid("")

    def test_verdict_mixed_case_fails(self):
        self._assert_verdict_invalid("Approve")


# ---------------------------------------------------------------------------
# Group 6: Array item type errors
# ---------------------------------------------------------------------------

class TestArrayItemTypeErrors(unittest.TestCase):
    """Items inside array fields must match the specified item type."""

    def test_files_with_integer_item_fails(self):
        sample = _minimal_valid()
        sample["files"] = ["valid.cs", 123]
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)

    def test_files_with_none_item_fails(self):
        sample = _minimal_valid()
        sample["files"] = ["valid.cs", None]
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)

    def test_concerns_with_integer_item_fails(self):
        sample = _minimal_valid()
        sample["concerns"] = ["Valid concern", 42]
        ok, errors = validate_sample(sample)
        self.assertFalse(ok)
        self.assertGreater(len(errors), 0)


# ---------------------------------------------------------------------------
# Group 7: CLI JSON-parse error handling
# ---------------------------------------------------------------------------

class TestCliJsonParseErrors(unittest.TestCase):
    """The __main__ CLI block must exit with code 1 on malformed JSON."""

    def _run_cli(self, stdin_text: str) -> int:
        """Run validate_schema.py as a subprocess and return its exit code."""
        script = str(_THIS_DIR / "validate_schema.py")
        result = subprocess.run(
            [sys.executable, script],
            input=stdin_text,
            capture_output=True,
            text=True,
        )
        return result.returncode

    def test_invalid_json_exits_nonzero(self):
        self.assertNotEqual(self._run_cli("{not valid json}"), 0)

    def test_truncated_json_exits_nonzero(self):
        self.assertNotEqual(self._run_cli('{"pr_number": '), 0)

    def test_bare_text_exits_nonzero(self):
        self.assertNotEqual(self._run_cli("not json"), 0)

    def test_valid_sample_exits_zero(self):
        self.assertEqual(self._run_cli(json.dumps(_minimal_valid())), 0)

    def test_invalid_sample_exits_nonzero(self):
        sample = _minimal_valid()
        del sample["pr_number"]
        self.assertNotEqual(self._run_cli(json.dumps(sample)), 0)

    def test_invalid_json_error_goes_to_stderr(self):
        script = str(_THIS_DIR / "validate_schema.py")
        result = subprocess.run(
            [sys.executable, script],
            input="{bad}",
            capture_output=True,
            text=True,
        )
        self.assertIn("ERROR", result.stderr)
        self.assertNotEqual(result.returncode, 0)


# ---------------------------------------------------------------------------
# Group 8: Schema cache behaviour
# ---------------------------------------------------------------------------

class TestSchemaCaching(unittest.TestCase):
    """_load_schema() must cache the schema so it is not re-read from disk."""

    def setUp(self):
        # Force a fresh load before each caching test to ensure isolation
        _module._SCHEMA_CACHE = None

    def test_schema_is_a_dict(self):
        schema = _module._load_schema()
        self.assertIsInstance(schema, dict)

    def test_schema_has_required_key(self):
        schema = _module._load_schema()
        self.assertIn("required", schema)

    def test_schema_cache_returns_same_object(self):
        first = _module._load_schema()
        second = _module._load_schema()
        self.assertIs(first, second)

    def tearDown(self):
        # Restore a valid cache so other tests are not affected by a missing cache
        _module._SCHEMA_CACHE = None


if __name__ == "__main__":
    unittest.main(verbosity=2)
