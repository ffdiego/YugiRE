"""Offline bounded command windows for CF-linked native resource candidates.

No hooks or native execution. Command context and two-sided LP samples are
exported, but no effect activation or causality is inferred.
"""
from __future__ import annotations
import argparse
import json
from pathlib import Path
from .effect_report import _candidate, _opcode, analyze_capture, gather


def view(e: dict, duel_index: int) -> dict:
    opcode = _opcode(e)
    candidate = _candidate(e, opcode)
    out = dict(seq=e.get("seq"), duelIndex=duel_index,
               opcode=f"0x{opcode:04X}", caller=e.get("caller"),
               threadId=e.get("threadId"),
               argsHex=[f"0x{x & 0xFFFFFFFF:08X}" for x in e.get("args", [])])
    if candidate:
        out["candidate"] = dict(kind=candidate[0], resourceId=candidate[1])
    observed = e.get("lifePointsObserved")
    if isinstance(observed, dict) and isinstance(observed.get("lifePoints"), list):
        out["lifePointsObserved"] = observed["lifePoints"]
    return out


def changes(commands: list[dict]) -> list[dict]:
    deltas, previous = [], None
    for command in commands:
        lp = command.get("lifePointsObserved")
        if not (isinstance(lp, list) and len(lp) == 2 and
                all(type(x) is int for x in lp)):
            continue
        if previous is not None:
            seq, before = previous
            delta = [lp[i] - before[i] for i in (0, 1)]
            if delta != [0, 0]:
                deltas.append(dict(beforeSeq=seq, afterSeq=command["seq"],
                                   lifeBefore=before, lifeAfter=lp, delta=delta))
        previous = (command["seq"], lp)
    return deltas



def lp_sample(entry: dict) -> list[int] | None:
    lp = entry.get("lifePointsObserved")
    return lp if (isinstance(lp, list) and len(lp) == 2 and
                  all(type(x) is int for x in lp)) else None


def paired_lp42_candidate(commands: list[dict], index: int) -> dict | None:
    """Recognize the exact CF +3/+4 pair observed for Rain of Mercy.

    The pair is command-level evidence, not a proven heal applied to LP.
    The prior sample may be stale: preserve its distance and avoid asserting
    causality for net deltas across other native actions.
    """
    cf = commands[index]
    seq = cf["seq"]
    duel = cf["duelIndex"]
    nearby = {c["seq"]: c for c in commands[index + 1:index + 12]
              if c["duelIndex"] == duel}
    pair = [nearby.get(seq + 3), nearby.get(seq + 4)]
    if any(e is None for e in pair):
        return None
    if any(e["opcode"] != "0x0042" or e["caller"] != "0x578823" or
           e["threadId"] != cf["threadId"] or len(e["argsHex"]) != 4
           for e in pair):
        return None
    words = [int(e["argsHex"][0], 16) for e in pair]
    targets = [(word >> 15) & 1 for word in words]
    amounts = [int(e["argsHex"][1], 16) for e in pair]
    if set(targets) != {0, 1} or amounts[0] != amounts[1] or amounts[0] <= 0:
        return None

    before = None
    before_index = None
    for k in range(index - 1, -1, -1):
        if commands[k]["duelIndex"] != duel:
            break
        lp = lp_sample(commands[k])
        if lp is not None:
            before_index = k
            before = dict(seq=commands[k]["seq"], lifePoints=lp,
                          commandsBetween=index - k - 1)
            break

    last_seq = pair[1]["seq"]
    after = None
    post_index = None
    for k in range(index + 1, len(commands)):
        entry = commands[k]
        if entry["duelIndex"] != duel:
            break
        if entry["seq"] > last_seq and lp_sample(entry) is not None:
            post_index = k
            after = dict(seq=entry["seq"], lifePoints=lp_sample(entry))
            break
    net = None if before is None or after is None else [
        after["lifePoints"][side] - before["lifePoints"][side] for side in (0, 1)
    ]
    interfering = []
    if before_index is not None and post_index is not None:
        interfering = [e["seq"] for e in commands[before_index + 1:post_index]
                       if e["duelIndex"] == duel and e["seq"] not in (seq + 3, seq + 4)
                       and e["opcode"] in ("0x0042", "0x0043")]
    return dict(kind="PairedNative0x42Commands", commandSeq=[e["seq"] for e in pair],
                caller="0x578823", targetSides=targets, amounts=amounts,
                beforeCfLpSample=before, firstLpSampleAfterPair=after,
                netLpDeltaBetweenSamples=net,
                otherLpCommandsBetweenSamples=interfering,
                interpretationLimit="Paired +1,000-like LP commands are observed, "
                                    "not verified healing outcomes. A previous LP sample "
                                    "may be stale; other effects and timing are unknown.")


def extract(inputs: list[Path], resource_ids: set[int], *,
            before: int = 12, after: int = 16) -> dict:
    if not resource_ids or any(type(i) is not int or i < 1 or i > 65535 for i in resource_ids):
        raise ValueError("resource IDs must be 1..65535")
    if before not in range(101) or after not in range(101):
        raise ValueError("before/after must be 0..100 commands")
    windows, captures = [], []
    for path in gather(inputs):
        checked = analyze_capture(path, resource_ids)
        if not checked["integrityOk"]:
            raise ValueError(f"Capture invalid: {path}: {checked['issues']}")
        captures.append(dict(file=path.name, sha256=checked["sha256"],
                             events=checked["events"],
                             terminalCommands=checked["terminalCommands"]))
        commands, duel = [], 0
        with path.open(encoding="utf-8") as source:
            for line in source:
                if not line.strip():
                    continue
                event = json.loads(line)
                opcode = _opcode(event)
                if opcode is None:
                    continue
                commands.append(view(event, duel))
                if opcode == 4:
                    duel += 1
        for i, event in enumerate(commands):
            candidate = event.get("candidate")
            if not candidate or candidate["kind"] != "CF" or candidate["resourceId"] not in resource_ids:
                continue
            nearby = [entry for entry in commands[max(0, i-before):i+after+1]
                      if entry["duelIndex"] == event["duelIndex"]]
            windows.append(dict(capture=path.name, sourceSha256=checked["sha256"],
                                resourceId=candidate["resourceId"], cfSeq=event["seq"],
                                duelIndex=event["duelIndex"], commands=nearby,
                                pairedLp42Candidate=paired_lp42_candidate(commands, i),
                                lpChangesWithinWindow=changes(nearby)))
    return dict(schema="yugire-native-cf-windows-v1",
                interpretationLimit="CF and LP changes are adjacent observations, "
                                    "not proof of effect activation or causality.",
                resourceIds=sorted(resource_ids), captures=captures, windows=windows)


def markdown(data: dict) -> str:
    lines = ["# Native CF command windows", "",
             f"Captures: {len(data['captures'])}; CF windows: {len(data['windows'])}.",
             "LP changes below do not establish causality.", ""]
    for window in data["windows"]:
        lines += [f"## Resource {window['resourceId']}, duel {window['duelIndex']+1}, "
                  f"CF seq {window['cfSeq']}", "",
                  "Observed LP changes (not attributed to the card):", ""]
        pair = window.get("pairedLp42Candidate")
        if pair:
            lines.append("Paired native 0x42 commands: "
                         + str(list(zip(pair["commandSeq"], pair["targetSides"], pair["amounts"]))))
            lines.append("Last earlier LP sample: " + str(pair["beforeCfLpSample"]))
            lines.append("First later LP sample: " + str(pair["firstLpSampleAfterPair"]))
            lines.append("Net change across samples, NOT proven effect: "
                         + str(pair["netLpDeltaBetweenSamples"]))
            lines.append("Other LP commands between samples: "
                         + str(pair["otherLpCommandsBetweenSamples"]))
            lines.append("")
        for change in window["lpChangesWithinWindow"]:
            lines.append(f"- Seq {change['beforeSeq']} -> {change['afterSeq']}: "
                         f"{change['lifeBefore']} -> {change['lifeAfter']}; "
                         f"delta {change['delta']}")
        lines += ["", "| Seq | Caller | Opcode | Args | LP |",
                  "|---:|---|---|---|---|"]
        for e in window["commands"]:
            lines.append(f"| {e['seq']} | {e['caller']} | {e['opcode']} | "
                         f"{', '.join(e['argsHex'])} | {e.get('lifePointsObserved', '-')} |")
        lines.append("")
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", nargs="+", type=Path, required=True)
    parser.add_argument("--resource-ids", nargs="+", type=int, required=True)
    parser.add_argument("--before", type=int, default=12)
    parser.add_argument("--after", type=int, default=16)
    parser.add_argument("--output-dir", type=Path, required=True)
    a = parser.parse_args(argv)
    try:
        if a.output_dir.exists():
            raise ValueError(f"Output directory exists: {a.output_dir}")
        result = extract(a.input, set(a.resource_ids), before=a.before, after=a.after)
        a.output_dir.mkdir(parents=True)
        (a.output_dir / "windows.json").write_text(
            json.dumps(result, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8", newline="\n")
        (a.output_dir / "windows.md").write_text(
            markdown(result), encoding="utf-8", newline="\n")
        print(f"CF windows: {len(result['windows'])}; output: {a.output_dir}")
        print("No effect causality asserted.")
        return 0
    except (OSError, ValueError, TypeError, KeyError, json.JSONDecodeError) as exc:
        parser.exit(2, f"Window extraction failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
