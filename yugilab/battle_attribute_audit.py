"""Offline calculated-vs-base battle stats, never proof of Sogen causality."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from .effect_report import gather
from .state_interactions import _apply_delta, _field_baseline

SCHEMA = "yugire-native-battle-attributes-audit-v1"
OBS_SCHEMA = "joey-command-entry-battle-attributes-v1"


def _attributes(document: dict) -> dict[int, dict]:
    if document.get("registryVersion") != 1:
        raise ValueError("Expected attributes registryVersion=1")
    result = {}
    for card in document["cards"]:
        ref = card.get("cardRef", "")
        if not isinstance(ref, str) or not ref.startswith("card:"):
            raise ValueError("Invalid attribute cardRef")
        idx = int(ref[5:])
        if idx in result or idx < 0 or idx > 4095:
            raise ValueError("Duplicate/out-of-range attribute index")
        result[idx] = card
    return result


def _context(sides: list[dict], spell_index: int) -> dict:
    spells, associations = [], []
    for side, player in enumerate(sides):
        field = player["fieldSlots"][10]
        if field["cardIndex"] == spell_index:
            spells.append(dict(side=side, slot=10,
                               occurrenceWord=field["occurrenceWord"]))
        for mon in player["fieldSlots"][:5]:
            if mon["cardIndex"] == 0:
                continue
            for a in mon.get("associations") or []:
                if a.get("referenceWord") in (0x0A00, 0x0A01):
                    associations.append(dict(side=side, slot=mon["slot"],
                        cardIndex=mon["cardIndex"], referenceWord=a["referenceWord"],
                        metadataWord=a.get("metadataWord")))
    return dict(fieldSpellObserved=spells,
                rawFieldAssociationCandidates=associations)


def _actor(block: dict, sides: list[dict], table: dict[int, dict]) -> dict:
    idx = block["cardIndexWord"]
    result = dict(blockIndex=block["blockIndex"],
                  cardIndexWord=idx,
                  descriptorWord=block["descriptorWord"],
                  calculatedAttack=block["calculatedAttack"],
                  calculatedDefense=block["calculatedDefense"],
                  damageWord=block["damageWord"],
                  fieldMatches=[
                      dict(side=side,slot=f["slot"],occurrenceWord=f["occurrenceWord"])
                      for side,player in enumerate(sides)
                      for f in player["fieldSlots"][:5]
                      if idx > 0 and f["cardIndex"] == idx])
    base = table.get(idx)
    if base is not None and type(base.get("attack")) is int and type(base.get("defense")) is int:
        result.update(baseAttack=base["attack"], baseDefense=base["defense"],
                      baseClassCode=base.get("classCode"),
                      calculatedMinusBaseAttack=block["calculatedAttack"]-base["attack"],
                      calculatedMinusBaseDefense=block["calculatedDefense"]-base["defense"])
    return result


def audit_capture(path: Path, table: dict[int, dict],
                  spell_index: int = 490) -> dict:
    sha = hashlib.sha256(path.read_bytes()).hexdigest()
    rows = [json.loads(s) for s in path.read_text(encoding="utf-8").splitlines()
            if s.strip()]
    if len(rows) < 3 or rows[0].get("kind") != "metadata" or rows[-1].get("kind") != "capture_summary":
        raise ValueError("Capture requires metadata, events and footer")
    head, foot = rows[0], rows[-1]
    if (foot.get("complete") is not True or foot.get("overwritten") != 0 or
            foot.get("stateTrace", {}).get("readErrors") != 0 or
            foot.get("battleAttributesTrace", {}).get("readErrors") != 0 or
            head.get("battleAttributesTrace", {}).get("enabled") is not True):
        raise ValueError("Incomplete or unreadable battle capture")
    samples, state, duel_index = [], None, -1
    for e in rows[1:-1]:
        if e.get("kind") != "command":
            continue
        obs = e.get("stateObservation") or {}
        if obs.get("kind") == "baseline":
            state = _field_baseline(obs["sides"])
            duel_index += 1
        elif obs.get("kind") == "delta":
            if state is None:
                raise ValueError("State delta without baseline")
            state = _apply_delta(state, obs["changes"])
        if e.get("opcodeMasked") != "0x0031":
            continue
        if state is None:
            raise ValueError("Missing state at battle producer entry")
        raw = e.get("battleAttributesObservation")
        if not isinstance(raw, dict) or raw.get("schema") != OBS_SCHEMA or (
                not isinstance(raw.get("blocks"), list) or len(raw["blocks"]) != 2):
            raise ValueError("Missing/incomplete battle stat blocks at 0x31")
        samples.append(dict(seq=e["seq"],duelIndex=duel_index,
                            selectorWord=raw["selectorWord"],
                            **_context(state,spell_index),
                            blocks=[_actor(x,state,table) for x in raw["blocks"]]))
    if not samples or len(samples) != foot["battleAttributesTrace"].get("samples"):
        raise ValueError("Battle samples disagree with footer")
    return dict(file=path.name,sha256=sha,duels=duel_index+1,
                battleSamples=len(samples),samples=samples)


def analyze(inputs: list[Path], attributes_path: Path, *,
            spell_index: int = 490) -> dict:
    if not 1 <= spell_index <= 4095:
        raise ValueError("Invalid field spell index")
    table = _attributes(json.loads(attributes_path.read_text(encoding="utf-8")))
    captures = [audit_capture(f,table,spell_index) for f in gather(inputs)]
    if not captures:
        raise ValueError("No captures")
    return dict(schema=SCHEMA,fieldSpellIndex=spell_index,
                attributesSha256=hashlib.sha256(attributes_path.read_bytes()).hexdigest(),
                captures=captures,
                interpretationLimit=(
                    "Calculated-vs-DAT differences do not identify a modifier source. "
                    "Battle block role, native computed value timing, attribute type-class "
                    "mapping, and any Sogen causal contribution await native validation."))


def markdown(result: dict) -> str:
    lines = ["# Atributos calculados de batalha", "",
             "Comparação observacional com atributos-base; não é evidência causal.", ""]
    for capture in result["captures"]:
        lines.extend([f"## {capture['file']}", "",
                      f"SHA256: {capture['sha256']}", "",
                      f"Amostras: {capture['battleSamples']}", "",
                      "| Seq | Duelo | Magia em campo | Dois blocos: índice ATK/DEF (diferença vs base) |",
                      "|---:|---:|---|---|"])
        for e in capture["samples"]:
            alive = ",".join(str(x["side"]) for x in e["fieldSpellObserved"]) or "não"
            blocks = "; ".join(
                f"{b['cardIndexWord']}: {b['calculatedAttack']}/{b['calculatedDefense']} "
                f"(delta {b.get('calculatedMinusBaseAttack','?')}/"
                f"{b.get('calculatedMinusBaseDefense','?')})"
                for b in e["blocks"])
            lines.append(f"| {e['seq']} | {e['duelIndex']+1} | {alive} | {blocks} |")
        lines.append("")
    lines.extend(["## Limites", "", result["interpretationLimit"], ""])
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--input",nargs="+",required=True,type=Path)
    p.add_argument("--attributes",required=True,type=Path)
    p.add_argument("--field-spell-index",type=int,default=490)
    p.add_argument("--output-dir",required=True,type=Path)
    a = p.parse_args(argv)
    try:
        if a.output_dir.exists():
            raise ValueError("Output directory already exists")
        result = analyze(a.input,a.attributes,spell_index=a.field_spell_index)
        a.output_dir.mkdir(parents=True)
        (a.output_dir/"battle-attributes.json").write_text(
            json.dumps(result,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
        (a.output_dir/"battle-attributes.md").write_text(
            markdown(result),encoding="utf-8")
        print("Battle blocks:",sum(c["battleSamples"] for c in result["captures"]),
              "(no card-effect causality inferred)")
        return 0
    except (ValueError,KeyError,TypeError,OSError,json.JSONDecodeError) as exc:
        p.exit(2,f"Battle attribute audit failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
