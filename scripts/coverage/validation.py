"""Restricted v1 schema validator and cross-record provenance gates; offline."""
import json
import re
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
SCHEMA = json.loads((ROOT / "docs/coverage/card-coverage.schema.json").read_text())
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

