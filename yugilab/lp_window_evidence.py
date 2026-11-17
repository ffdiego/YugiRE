"""Offline, provenance-bound Rain of Mercy LP evidence from native CF windows.

Window evidence is a bounded observation, not spell activation, graveyard
movement, trap status, or proof that the TCG description governs Joey.
"""
from __future__ import annotations

from collections import defaultdict

RAIN_RID = 1315


def correlate_rain_lp_windows(windows: dict, captures: list[dict],
                              examples: dict[int, list[dict]]) -> dict[int, list[dict]]:
    """Validate window provenance and extract only strictly scoped +1000/+1000 cases."""
    if windows.get("schema") != "yugire-native-cf-windows-v1":
        raise ValueError("Expected native CF windows schema v1")
    source_by_name = {c["file"]: c for c in captures}
    if len(source_by_name) != len(captures):
        raise ValueError("Ambiguous native capture filenames")
    for source in windows.get("captures", []):
        match = source_by_name.get(source.get("file"))
        if (match is None or source.get("sha256") != match["sha256"] or
            source.get("terminalCommands") != match["terminalCommands"] or
            source.get("events") != match["events"] or
            not match["integrityOk"]):
            raise ValueError(f"LP window capture provenance/integrity mismatch: "
                             f"{source.get('file')}")
    if not windows.get("captures"):
        raise ValueError("No source capture for native LP windows")
    valid_hashes = {(entry["file"], entry["sha256"]) for entry in windows["captures"]}
    found = defaultdict(list)
    used = set()
    for window in windows.get("windows", []):
        if (window.get("capture"), window.get("sourceSha256")) not in valid_hashes:
            raise ValueError("LP window source hash not among verified captures")
        rid = window.get("resourceId")
        seq = window.get("cfSeq")
        duel = window.get("duelIndex")
        key = (window.get("capture"), duel, seq)
        if key in used:
            raise ValueError("Duplicate CF window event")
        used.add(key)
        known = any(x["file"] == window["capture"] and x["duelIndex"] == duel
                    and x["seq"] == seq and x["kind"] == "CF"
                    for x in examples.get(rid, []))
        if not known:
            raise ValueError(f"LP window has no matching native CF example: {key}")
        if rid != RAIN_RID:
            continue
        pair = window.get("pairedLp42Candidate")
        if not isinstance(pair, dict):
            continue
        if (pair.get("kind") != "PairedNative0x42Commands" or
            pair.get("caller") != "0x578823" or
            pair.get("commandSeq") != [seq + 3, seq + 4] or
            pair.get("targetSides") != [0, 1] or
            pair.get("amounts") != [1000, 1000] or
            pair.get("otherLpCommandsBetweenSamples") != []):
            continue
        before, after = pair.get("beforeCfLpSample"), pair.get("firstLpSampleAfterPair")
        if not isinstance(before, dict) or not isinstance(after, dict):
            continue
        b, a = before.get("lifePoints"), after.get("lifePoints")
        if (not isinstance(b, list) or not isinstance(a, list) or
            len(b) != 2 or len(a) != 2 or
            any(type(value) is not int for value in b + a) or
            type(before.get("seq")) is not int or
            type(after.get("seq")) is not int or
            before["seq"] >= seq or after["seq"] <= seq + 4):
            continue
        delta = [a[i] - b[i] for i in (0, 1)]
        if delta != [1000, 1000] or pair.get("netLpDeltaBetweenSamples") != delta:
            continue
        # Capture snapshots may be 21-22 native commands older than CF;
        # retain separation instead of labeling causality as proved.
        found[rid].append(dict(
            capture=window["capture"], sourceSha256=window["sourceSha256"],
            duelIndex=duel, cfSeq=seq, pairCommandSeq=pair["commandSeq"],
            previousLpSampleSeq=before["seq"], nextLpSampleSeq=after["seq"],
            commandsBetweenPreviousSampleAndCf=before.get("commandsBetween"),
            beforeLp=b, afterLp=a, observedNetDelta=delta,
            evidence="Paired0x42CommandsAndMatchingNetLpSamples",
            limitation="Net +1000 for both players between snapshots correlates "
                       "with paired commands. The earlier sample may be stale; "
                       "neither full card activation nor independent causality "
                       "or interactions are proven."
        ))
    return dict(found)
