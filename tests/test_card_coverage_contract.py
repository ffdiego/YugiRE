"""Offline contract checks; no game files, engine execution or report generator.

The structural checker intentionally implements ONLY the JSON Schema vocabulary
used by this document. It rejects unhandled keywords. It is a test helper, not a
replacement for a general Draft 2020-12 validator or the future registry reader.
"""
import copy
import hashlib
import json
from pathlib import Path
import re
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = json.loads((ROOT / "docs/coverage/card-coverage.schema.json").read_text())
EXAMPLES_PATH = ROOT / "docs/coverage/card-coverage.examples.json"
EXAMPLE = json.loads(EXAMPLES_PATH.read_text())
KEYWORDS = {
    "$schema", "$id", "$defs", "$ref", "title", "default", "type", "properties",
    "required", "additionalProperties", "items", "uniqueItems", "minItems",
    "maxItems", "minLength", "minimum", "maximum", "pattern", "enum", "const",
    "allOf", "anyOf", "not", "if", "then",
}


def check_vocabulary(schema):
    assert not set(schema) - KEYWORDS, f"Unhandled schema keywords: {set(schema) - KEYWORDS}"
    for key in ("properties", "$defs"):
        for child in schema.get(key, {}).values():
            check_vocabulary(child)
    for key in ("items", "not", "if", "then"):
        if key in schema:
            check_vocabulary(schema[key])
    for key in ("allOf", "anyOf"):
        for child in schema.get(key, []):
            check_vocabulary(child)


def matches(value, schema):
    try:
        check_shape(value, schema)
        return True
    except ValueError:
        return False


def check_shape(value, schema, path="$"):
    def need(condition, message):
        if not condition:
            raise ValueError(f"{path}: {message}")

    if "$ref" in schema:
        need(schema["$ref"].startswith("#/$defs/"), "Only local definitions allowed")
        check_shape(value, SCHEMA["$defs"][schema["$ref"].split("/")[-1]], path)
    if "type" in schema:
        types = {"object": dict, "array": list, "string": str,
                 "integer": int, "null": type(None), "boolean": bool}
        need(type(value) is types[schema["type"]], "Wrong type")
    if "const" in schema:
        need(type(value) is type(schema["const"]) and value == schema["const"], "Wrong constant")
    if "enum" in schema:
        need(value in schema["enum"], "Unknown enum")
    if isinstance(value, dict):
        need(set(schema.get("required", [])) <= value.keys(), "Missing required field")
        if schema.get("additionalProperties") is False:
            need(value.keys() <= schema["properties"].keys(), "Unexpected field")
        for key, child in schema.get("properties", {}).items():
            if key in value:
                check_shape(value[key], child, f"{path}/{key}")
    if isinstance(value, list):
        need(len(value) >= schema.get("minItems", 0), "Too few items")
        need(len(value) <= schema.get("maxItems", len(value)), "Too many items")
        if schema.get("uniqueItems"):
            need(len({json.dumps(x, sort_keys=True) for x in value}) == len(value), "Duplicate items")
        if "items" in schema:
            for index, child in enumerate(value):
                check_shape(child, schema["items"], f"{path}/{index}")
    if isinstance(value, str):
        need(len(value) >= schema.get("minLength", 0), "Short string")
        if "pattern" in schema:
            need(re.search(schema["pattern"], value) is not None, "Invalid pattern")
    if type(value) is int:
        need(value >= schema.get("minimum", value), "Below minimum")
        need(value <= schema.get("maximum", value), "Above maximum")
    for child in schema.get("allOf", []):
        check_shape(value, child, path)
    if "anyOf" in schema:
        need(any(matches(value, x) for x in schema["anyOf"]), "No alternative matches")
    if "not" in schema:
        need(not matches(value, schema["not"]), "Forbidden value")
    if "if" in schema and matches(value, schema["if"]):
        check_shape(value, schema.get("then", {}), path)


def check_relations(registry):
    """Executable cross-record invariants, beyond JSON Schema's local checks."""
    def need(condition, message):
        if not condition:
            raise ValueError(message)

    tables = {}
    for kind in ("cards", "callbacks", "mechanics", "aliases", "evidence"):
        rows = registry[kind]
        tables[kind] = {x["id"]: x for x in rows}
        need(len(tables[kind]) == len(rows), f"Duplicate {kind} id")
    cards, callbacks, mechanics, evidence = (tables[k] for k in ("cards", "callbacks", "mechanics", "evidence"))
    indices = [c["identity"]["internalIndex"] for c in cards.values()]
    need(len(set(indices)) == len(indices), "Duplicate internal index")
    for key in ("row", "resourceId"):
        need(len({c[key] for c in callbacks.values()}) == len(callbacks), "Duplicate callback row/resource")
    expected = registry["catalogScope"]["expectedCardCount"]
    need(expected is None or expected == len(cards), "Wrong denominator")

    def refs(ids, table):
        need(all(x in table for x in ids), "Dangling reference")

    def coverage(cov):
        for axis, a in cov.items():
            refs(a["evidenceRefs"], evidence)
            if axis == "implementation" and a["state"] != "Unknown":
                need(any(evidence[r]["kind"] == "SourceInspection" and
                         evidence[r]["location"]["commit"] == registry["baselineCommit"]
                         for r in a["evidenceRefs"]), "Implementation must be evidenced at baseline")
        v = cov["validation"]
        if v["state"] != "ValidatedInScope":
            return
        need(len({x["scope"] for x in cov.values()}) == 1, "Validated axes must share scope")
        receipt = v["acceptance"]
        required = [receipt["matrixEvidenceRef"], receipt["reviewEvidenceRef"],
                    *receipt["nativeEvidenceRefs"], *receipt["testEvidenceRefs"]]
        refs(required, evidence)
        need(set(required) <= set(v["evidenceRefs"]), "Receipt evidence must be explicit")
        need(evidence[receipt["matrixEvidenceRef"]]["kind"] == "ValidationMatrix", "Missing matrix")
        need(evidence[receipt["reviewEvidenceRef"]]["kind"] == "Review", "Missing review")
        for r in receipt["testEvidenceRefs"]:
            need(evidence[r]["kind"] == "SyntheticTest", "Missing regression evidence")
        for r in receipt["nativeEvidenceRefs"]:
            e = evidence[r]
            cap = e["capture"]
            need(e["kind"] == "NativeObservation" and cap is not None, "Missing native observation")
            need(cap["sha256"] is not None and cap["exeSha256"] == registry["fingerprints"]["exeSha256"]
                 and cap["attestation"] != "Unknown" and cap["completeness"] == "Complete",
                 "Incomplete or unattested native evidence")

    for e in evidence.values():
        path = e["location"]["path"]
        need(not path.startswith("/") and ".." not in path.split("/") and "\\" not in path,
             "Evidence path must be repository-relative")
        if e["kind"] == "NativeObservation":
            need(e["capture"] is not None, "Native evidence needs explicit capture provenance")
        else:
            need(e["capture"] is None, "Non-native evidence cannot masquerade as capture")
        if e["capture"] is not None and e["capture"]["attestation"] != "Unknown":
            need(e["capture"]["exeSha256"] is not None, "Attestation requires EXE hash")
    for callback in callbacks.values():
        refs(callback["evidenceRefs"], evidence)
    for m in mechanics.values():
        coverage(m["coverage"])
        refs(m["dependencies"], mechanics)
        if m["coverage"]["validation"]["state"] == "ValidatedInScope":
            need(not m["openQuestions"], "Validated mechanic has open questions")
            need(all(mechanics[d]["coverage"]["validation"]["state"] == "ValidatedInScope"
                     for d in m["dependencies"]), "Unvalidated mechanic dependency")
    def visit(id, ancestors):
        need(id not in ancestors, "Cyclic mechanic dependencies")
        for dep in mechanics[id]["dependencies"]:
            visit(dep, ancestors | {id})
    for id in mechanics:
        visit(id, set())
    for c in cards.values():
        need(c["id"] == f'card:{c["identity"]["internalIndex"]}', "Card key must retain index")
        coverage(c["coverage"])
        lookup = c["callbackLookup"]
        refs(lookup["evidenceRefs"], evidence)
        refs(lookup["callbackRefs"], callbacks)
        for r in lookup["callbackRefs"]:
            need(callbacks[r]["resourceId"] == c["identity"]["resourceId"], "Callback resource mismatch")
        ids = [b["mechanicRef"] for b in c["mechanics"]]
        need(len(set(ids)) == len(ids), "Duplicate binding")
        for b in c["mechanics"]:
            refs([b["mechanicRef"]], mechanics)
            refs(b["evidenceRefs"], evidence)
            refs(b["callbackRefs"], callbacks)
            need(set(b["callbackRefs"]) <= set(lookup["callbackRefs"]), "Foreign callback in binding")
            coverage(b["coverage"])
            if b["relation"] == "Candidate":
                need(b["coverage"]["validation"]["state"] != "ValidatedInScope", "Candidate cannot be validated")
        if c["coverage"]["validation"]["state"] == "ValidatedInScope":
            for b in c["mechanics"]:
                need(b["relation"] == "Established" and b["coverage"]["validation"]["state"] == "ValidatedInScope",
                     "Unvalidated card binding")
                need(set(mechanics[b["mechanicRef"]]["dependencies"]) <= set(ids), "Missing required dependency")
    for alias in tables["aliases"].values():
        refs([alias["leftCardRef"], alias["rightCardRef"]], cards)
        refs(alias["evidenceRefs"], evidence)
        need(alias["leftCardRef"] != alias["rightCardRef"], "Self alias")
        left = cards[alias["leftCardRef"]]["identity"]["resourceId"]
        right = cards[alias["rightCardRef"]]["identity"]["resourceId"]
        if alias["relation"] == "SameResourceId":
            need(left == right, "False identical resource alias")
        if alias["relation"] == "Plus2000Variant":
            need(abs(left - right) == 2000, "False +2000 variant")


def validate(registry):
    check_shape(registry, SCHEMA)
    check_relations(registry)


class CoverageContractTests(unittest.TestCase):
    def reject(self, change):
        value = copy.deepcopy(EXAMPLE)
        change(value)
        with self.assertRaises(ValueError):
            validate(value)

    def test_schema_vocabulary_and_examples(self):
        check_vocabulary(SCHEMA)
        validate(EXAMPLE)
        with self.assertRaises(AssertionError):
            check_vocabulary({"unimplementedKeyword": True})

    def test_unknown_is_explicit_default_without_implicit_promotion(self):
        cov = copy.deepcopy(EXAMPLE["cards"][0]["coverage"])
        for axis, value in cov.items():
            self.assertEqual(SCHEMA["$defs"][axis]["properties"]["state"]["default"], "Unknown")
            value.update(state="Unknown", evidenceRefs=[])
        check_shape(cov, SCHEMA["$defs"]["coverage"])
        del cov["validation"]["state"]
        with self.assertRaises(ValueError):
            check_shape(cov, SCHEMA["$defs"]["coverage"])

    def test_rejects_unknown_fields_enums_versions_and_missing_fields(self):
        for change in [lambda r: r.update(schemaVersion=2),
                       lambda r: r.update(timestamp="today"),
                       lambda r: r["cards"][0]["coverage"]["validation"].update(state="Parity"),
                       lambda r: r["cards"][0]["coverage"].pop("understanding")]:
            with self.subTest(change=change):
                self.reject(change)

    def test_rejects_invalid_fingerprints_and_id_domains(self):
        for field, value in [("resourceId", -1), ("resourceId", 65535), ("resourceId", True),
                             ("internalIndex", -1)]:
            self.reject(lambda r: r["cards"][0]["identity"].update({field: value}))
        self.reject(lambda r: r["fingerprints"].update(exeSha256="unknown"))
        self.reject(lambda r: r["callbacks"][0].update(rowVa="0x584500"))

    def test_zero_id_empty_name_and_duplicate_resource_remain_distinct(self):
        r = copy.deepcopy(EXAMPLE)
        second = copy.deepcopy(r["cards"][0])
        second.update(id="card:0", mechanics=[])
        second["identity"].update(internalIndex=0, name="")
        r["cards"].append(second)
        r["catalogScope"]["expectedCardCount"] = 6
        r["aliases"].append(dict(id="alias:duplicate",leftCardRef="card:24",rightCardRef="card:0",
                                relation="SameResourceId",scope="Synthetic duplicate resource, distinct rows",evidenceRefs=["identities"]))
        validate(r)
        r["aliases"] = []
        second["identity"]["resourceId"] = 0
        validate(r)
        second["identity"]["resourceId"] = 2077
        r["aliases"].append(dict(id="alias:variant",leftCardRef="card:24",rightCardRef="card:0",
                                relation="Plus2000Variant",scope="Synthetic helper-only relation",evidenceRefs=["attributes"]))
        validate(r)
        self.assertEqual(len(r["cards"]), 6)

    def test_rejects_duplicates_dangling_refs_and_foreign_callbacks(self):
        for change in [lambda r: r["cards"].append(copy.deepcopy(r["cards"][0])),
                       lambda r: r["evidence"].append(copy.deepcopy(r["evidence"][0])),
                       lambda r: r["cards"][0]["coverage"]["understanding"].update(evidenceRefs=["missing"]),
                       lambda r: r["cards"][1]["callbackLookup"].update(callbackRefs=["callback:24"]),
                       lambda r: r["cards"][0]["mechanics"][0].update(mechanicRef="missing"),
                       lambda r: r["mechanics"][0].update(dependencies=[r["mechanics"][0]["id"]])]:
            with self.subTest(change=change):
                self.reject(change)

    def test_absent_null_and_unknown_are_distinct(self):
        self.assertEqual(EXAMPLE["cards"][0]["callbackLookup"]["state"], "AbsentInTable")
        self.assertIsNone(EXAMPLE["callbacks"][0]["slots"]["availability"])
        self.reject(lambda r: r["cards"][0]["callbackLookup"].update(callbackRefs=["callback:24"]))
        r = copy.deepcopy(EXAMPLE)
        r["cards"][0]["callbackLookup"].update(state="Unknown", evidenceRefs=[])
        validate(r)
        r["callbacks"][0]["slots"]["availability"] = 0
        with self.assertRaises(ValueError):
            validate(r)

    def test_nonunknown_claim_needs_evidence_and_implementation_baseline(self):
        self.reject(lambda r: r["cards"][0]["coverage"]["understanding"].update(evidenceRefs=[]))
        self.reject(lambda r: r["cards"][0]["coverage"]["implementation"].update(evidenceRefs=["research-policy"]))

    def test_no_card_is_validated_and_conditional_match_is_scoped(self):
        self.assertEqual([c["coverage"]["validation"]["state"] for c in EXAMPLE["cards"]], ["Unknown"] * 5)
        axe = EXAMPLE["cards"][0]["mechanics"][0]["coverage"]
        self.assertEqual(axe["validation"]["state"], "ConditionalMatch")
        for change in [lambda r: r["cards"][0]["coverage"]["validation"].update(state="ValidatedInScope"),
                       lambda r: r["cards"][0]["mechanics"][0]["coverage"]["validation"].update(state="ValidatedInScope")]:
            self.reject(change)

    def test_shared_callback_does_not_change_semantic_coverage(self):
        catalog = json.loads((ROOT / "docs/evidence/card-callback-catalog.json").read_text())
        sparks, ookazi = [next(r for r in catalog["records"] if r["resource_id"] == id)
                          for id in (342, 345)]
        self.assertEqual(sparks["profile"], ookazi["profile"])
        r = copy.deepcopy(EXAMPLE)
        witch = r["callbacks"][2]
        witch["slots"] = copy.deepcopy(r["callbacks"][1]["slots"])
        witch["profileId"] = r["callbacks"][1]["profileId"]
        # A structurally legal synthetic pointer grouping cannot supply evidence
        # for a promotion. Actual examples are pinned to the real table below.
        validate(r)
        self.assertEqual(r["cards"][1]["coverage"]["understanding"]["state"], "Unknown")
        self.assertEqual(r["cards"][1]["coverage"]["validation"]["state"], "Unknown")

    def test_promotion_requires_native_provenance_review_and_closed_card(self):
        # A synthetic acceptance document tests the gates, never enters examples.
        r = copy.deepcopy(EXAMPLE)
        native = copy.deepcopy(next(e for e in r["evidence"] if e["id"] == "axe-buffer"))
        native.update(id="synthetic-native-receipt")
        native["capture"].update(completeness="Complete", attestation="External",
                                 exeSha256=r["fingerprints"]["exeSha256"])
        r["evidence"].append(native)
        for id, kind in [("matrix", "ValidationMatrix"), ("review", "Review")]:
            e = copy.deepcopy(r["evidence"][0])
            e.update(id=id, kind=kind, capture=None)
            r["evidence"].append(e)
        c = r["cards"][0]
        c.update(mechanicInventory="Closed", openQuestions=[])
        for cov in [c["coverage"], c["mechanics"][0]["coverage"]]:
            for axis, state in [("identification", "Identified"), ("understanding", "UnderstoodInScope"),
                                ("implementation", "ImplementedInScope")]:
                cov[axis].update(state=state, limitations=[])
            cov["validation"].update(state="ValidatedInScope",limitations=[],
                evidenceRefs=["matrix","review","synthetic-native-receipt","attribute-tests"],
                acceptance=dict(matrixEvidenceRef="matrix",reviewEvidenceRef="review",
                    nativeEvidenceRefs=["synthetic-native-receipt"],testEvidenceRefs=["attribute-tests"],
                    independentInputs=True,completeContext=True,allRequiredCasesPassed=True))
        validate(r)
        for mutate in [lambda x: x["cards"][0].update(mechanicInventory="Open"),
                       lambda x: x["cards"][0]["mechanics"][0].update(relation="Candidate"),
                       lambda x: x["cards"][0]["mechanics"][0]["coverage"]["validation"].update(state="ConditionalMatch",acceptance=None),
                       lambda x: next(e for e in x["evidence"] if e["id"] == "synthetic-native-receipt")["capture"].update(sha256=None),
                       lambda x: next(e for e in x["evidence"] if e["id"] == "synthetic-native-receipt")["capture"].update(exeSha256="0" * 64),
                       lambda x: next(e for e in x["evidence"] if e["id"] == "synthetic-native-receipt")["capture"].update(completeness="Incomplete"),
                       lambda x: next(e for e in x["evidence"] if e["id"] == "review").update(kind="Inference")]:
            bad = copy.deepcopy(r)
            mutate(bad)
            with self.subTest(mutation=mutate), self.assertRaises(ValueError):
                validate(bad)

    def test_pinned_callbacks_and_identity_sources(self):
        catalog = json.loads((ROOT / "docs/evidence/card-callback-catalog.json").read_text())
        for cb in EXAMPLE["callbacks"]:
            record = catalog["records"][cb["row"]]
            profile = next(p for p in catalog["profiles"] if p["id"] == record["profile"])
            self.assertEqual((record["resource_id"], record["profile"]), (cb["resourceId"], cb["profileId"]))
            self.assertEqual(int(cb["rowVa"], 16), int(catalog["table"]["va"], 16) + cb["row"] * catalog["table"]["stride"])
            self.assertEqual([int(cb["slots"][s], 16) if cb["slots"][s] else None for s in catalog["slot_order"]],
                             [int(p, 16) if p else None for p in profile["callbacks"]])
        for c in EXAMPLE["cards"]:
            if c["callbackLookup"]["state"] == "AbsentInTable":
                self.assertNotIn(c["identity"]["resourceId"], [x["resource_id"] for x in catalog["records"]])
        self.assertEqual([(c["identity"]["internalIndex"], c["identity"]["resourceId"]) for c in EXAMPLE["cards"]],
                         [(24, 77), (261, 573), (515, 1523), (614, 300), (755, 342)])
        self.assertEqual(EXAMPLE["fingerprints"]["exeSha256"], catalog["target_sha256"])

    def test_source_digests_and_external_research_are_pinned(self):
        external = []
        for e in EXAMPLE["evidence"]:
            loc = e["location"]
            if loc["commit"] == EXAMPLE["baselineCommit"]:
                # Hash immutable blob bytes, independent of Windows autocrlf.
                blob = subprocess.check_output(
                    ["git", "show", f'{loc["commit"]}:{loc["path"]}'], cwd=ROOT)
                self.assertEqual(hashlib.sha256(blob).hexdigest(), loc["sha256"], loc["path"])
            else:
                external.append(loc)
        self.assertEqual(len(external), 2)
        self.assertTrue(all(e["commit"] == "a7de8c016af3d4c9c857b4be8c8bb6a6b1af9bfb" for e in external))

    def test_example_serialization_and_declared_counts_are_deterministic(self):
        canonical = (json.dumps(EXAMPLE, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode()
        self.assertEqual(EXAMPLES_PATH.read_bytes(), canonical)
        self.assertEqual([c["identity"]["internalIndex"] for c in EXAMPLE["cards"]], sorted(c["identity"]["internalIndex"] for c in EXAMPLE["cards"]))
        for kind in ("mechanics", "aliases", "evidence"):
            self.assertEqual([x["id"] for x in EXAMPLE[kind]], sorted(x["id"] for x in EXAMPLE[kind]))
        for c in EXAMPLE["cards"]:
            ids = [b["mechanicRef"] for b in c["mechanics"]]
            self.assertEqual(ids, sorted(ids))
        def ordered_refs(value):
            if isinstance(value, dict):
                for key, child in value.items():
                    if key.endswith("Refs") or key == "dependencies":
                        self.assertEqual(child, sorted(child))
                    ordered_refs(child)
            elif isinstance(value, list):
                for child in value:
                    ordered_refs(child)
        ordered_refs(EXAMPLE)
        self.assertEqual(len(EXAMPLE["cards"]), EXAMPLE["catalogScope"]["expectedCardCount"])
        self.assertEqual(sum(c["callbackLookup"]["state"] == "Present" for c in EXAMPLE["cards"]), 3)
        self.assertEqual(sum(c["mechanicInventory"] == "Closed" for c in EXAMPLE["cards"]), 0)
        self.assertEqual(sum(c["coverage"]["validation"]["state"] == "ValidatedInScope" for c in EXAMPLE["cards"]), 0)
        for axis in ("understanding", "implementation"):
            self.assertEqual(sum(c["coverage"][axis]["state"] == "Partial" for c in EXAMPLE["cards"]), 3)
            self.assertEqual(sum(c["coverage"][axis]["state"] == "Unknown" for c in EXAMPLE["cards"]), 2)
        bindings = [b for c in EXAMPLE["cards"] for b in c["mechanics"]]
        self.assertEqual(len(bindings), 6)
        for state, count in [("ConditionalMatch", 1), ("SyntheticOnly", 3), ("ObservedOnly", 1), ("Unknown", 1)]:
            self.assertEqual(sum(b["coverage"]["validation"]["state"] == state for b in bindings), count)


if __name__ == "__main__":
    unittest.main()
