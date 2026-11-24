#!/usr/bin/env python3
"""C01: classify local DAT descriptions; never persist the descriptions."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FAMILIES = {
    'draw': r'\bdraw\b', 'destroy': r'\bdestroy(?:ed|s)?\b',
    'flip': r'\bflip\b', 'graveyard': r'\bgraveyard\b',
    'negation': r'\bnegat\w*\b', 'stat-change': r'\b(?:atk|def|attack points|defense points)\b',
    'search': r'\b(?:search|select|choose)\b.*\bdeck\b|\bfrom (?:your|the) deck\b',
    'return': r'\breturn\w*\b', 'position': r'\b(?:battle|attack|defense) position\b',
    'damage': r'\b(?:damage|inflict\w*)\b', 'heal': r'\b(?:increase|recover|gain|restore)\w*\b.*\blife points\b',
    'discard': r'\bdiscard\w*\b', 'banish': r'\b(?:remove\w* from (?:the )?(?:play|game)|banish\w*)\b',
    'summon': r'\bsummon\w*\b', 'control': r'\bcontrol\b', 'equip': r'\bequip\w*\b',
    'random': r'\b(?:random\w*|coin|dice)\b', 'restriction': r'\b(?:cannot|can not|may not)\b',
}
RESPONSE = r'\b(?:when|whenever|during|each time|if|unless|until|after|before|chain|activate|activation|negat\w*|counter|flip|phase|pay|tribute|discard|select|choose|target|random\w*|coin|dice|equip\w*|cannot|can not)\b'
# Full-text allowlist, not a keyword heuristic: no additional clause may pass.
SIMPLE = {
    'damage': r'(?:inflict|deal) [0-9]+ (?:points? of )?damage to (?:your opponent(?:\'s life points)?|the opponent(?:\'s life points)?)',
    'heal': r'(?:increase|recover|restore) your life points by [0-9]+(?: points)?|gain [0-9]+ life points',
    'draw': r'draw (?:[0-9]+|one|two|three) cards?(?: from your deck)?',
}


def classify(description, kind):
    text = ' '.join(description.lower().replace('’', "'").split())
    families = sorted(name for name, pattern in FAMILIES.items() if re.search(pattern, text))
    if kind == 'Normal':
        return [], 'not-applicable'
    if not text or kind == 'Other':
        return families, 'review'
    if kind in ('Trap', 'Fusion', 'Ritual') or re.search(RESPONSE, text):
        return families, 'queue-or-response'
    if kind == 'Spell':
        for name, pattern in SIMPLE.items():
            if re.fullmatch('(?:' + pattern + r')\.?', text):
                return sorted(set(families + [name])), 'trivial-candidate'
    return families, 'review'


def build(extracted, subtypes, callbacks, tracked):
    rows, seen = [], set()
    for card in extracted['cards']:
        if not card['pack'] & 7:
            continue
        idx, rid, code = (card[k] for k in ('internalIndex', 'resourceId', 'classCode'))
        if idx in seen:
            raise ValueError('Duplicate internal index')
        seen.add(idx)
        kind = subtypes.get(str(rid), 'EffectOrUnknown') if 1 <= code <= 20 else {21: 'Trap', 22: 'Spell'}.get(code, 'Other')
        families, route = classify(card['description'], kind)
        rows.append(dict(internalIndex=idx, resourceId=rid, packMask=card['pack'], kind=kind,
                         families=families, route=route, callbackProfile=callbacks.get(rid),
                         currentStatus=tracked.get(rid, 'Pending')))
    rows.sort(key=lambda r: r['internalIndex'])
    return dict(schemaVersion=1, fingerprints=extracted['fingerprints'], packSha256=extracted['packSha256'],
                routes=dict(Counter(r['route'] for r in rows)), cards=rows)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--dat', type=Path, required=True)
    p.add_argument('--output', type=Path, default=ROOT / 'scratch/card-taxonomy.json')
    a = p.parse_args()
    if not a.dat.is_file():
        p.error('Local DAT unavailable')
    project = str(ROOT / 'src/Joey.Coverage')
    subprocess.run(['dotnet', 'build', project, '-c', 'Release'], cwd=ROOT, check=True)
    # Raw descriptions travel only via a private in-memory pipe; never log stdout.
    proc = subprocess.run(['dotnet', 'run', '--project', project, '-c', 'Release', '--no-build', '--',
                           str(a.dat.resolve()), '--taxonomy-input'], cwd=ROOT, stdout=subprocess.PIPE, check=True)
    try:
        extracted = json.loads(proc.stdout)
    except (ValueError, UnicodeError):
        raise SystemExit('Invalid local taxonomy input; no raw output logged') from None
    def read(path):
        return json.loads((ROOT / path).read_text(encoding='utf-8'))
    subtypes = read('docs/cards/monster-subtypes.json')['subtypes']
    callbacks = {r['resource_id']: r['profile'] for r in read('docs/evidence/card-callback-catalog.json')['records']}
    tracked = {rid: g['status'] for g in read('docs/cards/playability-manifest.json')['groups'] for rid in g['resourceIds']}
    result = build(extracted, subtypes, callbacks, tracked)
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(result, indent=2, sort_keys=True) + '\n', encoding='utf-8')
    print(f"Classified {len(result['cards'])} pack entries: {result['routes']}")


if __name__ == '__main__':
    main()
