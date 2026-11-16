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
