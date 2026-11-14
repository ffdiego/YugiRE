#!/usr/bin/env python3
"""Reproducible inventory with optional, separate reviewed semantic overlays."""
import argparse
import csv
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from scripts.coverage.validation import validate, check_vocabulary, SCHEMA

SOURCE_COMMIT = '7e5ca132f7185bd2c3df4c9782e080fb95a135f5'
CALLBACK_PATH = 'docs/evidence/card-callback-catalog.json'
AXES = ('identification', 'understanding', 'implementation', 'validation')
SLOTS = ('primary', 'candidate', 'availability', 'preparation', 'target_preparation')


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + '\n').encode('utf-8')


def digest(data):
    return hashlib.sha256(data).hexdigest()


def blob(path):
    return subprocess.check_output(['git', 'show', f'{SOURCE_COMMIT}:{path}'], cwd=ROOT)


def evidence(id, path, kind, claim):
    return dict(id=id, kind=kind, claim=claim, capture=None,
                limitations=['Static inventory only; no semantic or dynamic equivalence claim.'],
                location=dict(repository='ffdiego/YugiRE', commit=SOURCE_COMMIT,
                              path=path, locator='Complete document', sha256=digest(blob(path))))


def unknown():
    result = {axis: dict(state='Unknown', scope='Whole card; semantics unaudited',
                        evidenceRefs=[], limitations=['Automatic extraction does not establish coverage.']) for axis in AXES}
    result['validation']['acceptance'] = None
    return result


def generate(extracted, catalog, baseline, ledger):
    """Pure transformation; synthetic inputs supported without local DAT or Git."""
    fp = dict(extracted['fingerprints'], exeSha256=catalog['target_sha256'])
    for a, b in [('cardIdSha256', 'card_id_sha256'), ('namesSha256', 'names_sha256'),
                 ('propertiesSha256', 'properties_sha256')]:
        if fp[a] != catalog[b]:
            raise ValueError('Callback/resource fingerprint mismatch: ' + a)
    if catalog['slot_order'] != list(SLOTS) or catalog['table']['stride'] != 24:
        raise ValueError('Unsupported callback layout')
    profiles = {p['id']: p for p in catalog['profiles']}
    if len(profiles) != len(catalog['profiles']):
        raise ValueError('Duplicate profile')
    callbacks = []
    for r in sorted(catalog['records'], key=lambda r: r['row']):
        pointers = profiles[r['profile']]['callbacks']
        if len(pointers) != 5:
            raise ValueError('Expected five pointers')
        callbacks.append(dict(id=f"callback:{r['row']}", row=r['row'], resourceId=r['resource_id'],
            profileId=r['profile'], rowVa=f"0x{int(catalog['table']['va'], 16) + r['row'] * 24:08x}",
            slots=dict(zip(SLOTS, [f"0x{int(p, 16):08x}" if p else None for p in pointers])), evidenceRefs=['callbacks']))
    if [r['row'] for r in callbacks] != list(range(catalog['table']['count'])):
        raise ValueError('Incomplete callback table')
    lookup = {r['resourceId']: r['id'] for r in callbacks}
    cards, attributes, aliases = [], [], []
    rows = sorted(extracted['cards'], key=lambda c: c['internalIndex'])
    for c in rows:
        for field, maximum in [('internalIndex', 2**31-1), ('resourceId', 65534), ('properties', 2**32-1), ('pack', 65535)]:
            if type(c[field]) is not int or not 0 <= c[field] <= maximum:
                raise ValueError('Invalid extracted ' + field)
        p = c['properties']
        if [c[k] for k in ('classCode', 'attack', 'defense', 'level')] != [(p >> 20) & 31, ((p >> 9) & 511)*10, (p & 511)*10, (p >> 25) & 15]:
            raise ValueError('Inconsistent attributes')
        ref = f"card:{c['internalIndex']}"
        cb = lookup.get(c['resourceId'])
        cards.append(dict(id=ref, identity={k: c[k] for k in ('internalIndex', 'resourceId', 'name')},
            coverage=unknown(), mechanicInventory='Open', mechanics=[],
            openQuestions=['Effect inventory unaudited, including rules outside callback table.'],
            callbackLookup=dict(state='Present' if cb else 'AbsentInTable',
                                callbackRefs=[cb] if cb else [], evidenceRefs=['callbacks'])))
        attributes.append(dict(cardRef=ref, **{k: c[k] for k in ('properties', 'classCode', 'attack', 'defense', 'level', 'pack')},
                               availability={game: bool(c['pack'] & bit) for game, bit in [('Yugi', 1), ('Kaiba', 2), ('Joey', 4)]}))
    for i, left in enumerate(rows):
        for right in rows[i+1:]:
            delta = abs(left['resourceId'] - right['resourceId'])
            if delta not in (0, 2000):
                continue
            aliases.append(dict(id=f"alias:{left['internalIndex']}:{right['internalIndex']}",
                leftCardRef=f"card:{left['internalIndex']}", rightCardRef=f"card:{right['internalIndex']}",
                relation='SameResourceId' if delta == 0 else 'Plus2000Variant',
                scope='Numeric resource relationship only; no semantic equivalence.', evidenceRefs=['extraction']))
    for pair in extracted.get('helperAliases', []):
        aliases.append(dict(id=f"alias:helper:{pair['leftIndex']}:{pair['rightIndex']}",
            leftCardRef=f"card:{pair['leftIndex']}", rightCardRef=f"card:{pair['rightIndex']}",
            relation='NativeHelperAlias', scope='NativeAiAttributes.EquivalentResources only; no effect equivalence.',
            evidenceRefs=['helper']))
    snapshot = dict(schemaVersion=1, kind='Inventory', baselineCommit=baseline, fingerprints=fp,
        catalogScope=dict(kind='AllNonSentinelRows', expectedCardCount=len(cards), selection='All rows returned by Catalog.Load; no card_pack filter'),
        cards=cards, callbacks=callbacks, mechanics=[], aliases=sorted(aliases, key=lambda a: a['id']),
        evidence=sorted(ledger, key=lambda e: e['id']))
    check_vocabulary(SCHEMA)
    validate(snapshot)
    summary = dict(catalogRows=len(cards), distinctResources=len({c['resourceId'] for c in rows}),
        callbackRows=len(callbacks), orphanCallbacks=[r['id'] for r in callbacks if r['resourceId'] not in {c['resourceId'] for c in rows}],
        packUnionRows=sum(bool(c['pack'] & 7) for c in rows),
        availabilityRows={g: sum(a['availability'][g] for a in attributes) for g in ('Yugi', 'Kaiba', 'Joey')},
        coverage={axis: {'Unknown': len(cards)} for axis in AXES}, knownMechanics=0,
        callbackLookup={s: sum(c['callbackLookup']['state'] == s for c in cards) for s in ('Present', 'AbsentInTable', 'Unknown')})
    auxiliary = dict(kind='ExtractedAttributes', registryVersion=1, fingerprints=fp, packSha256=extracted['packSha256'],
                     evidenceRefs=['extraction', 'pack'], cards=attributes, summary=summary)
    return snapshot, auxiliary


def csv_bytes(snapshot, auxiliary):
    stream = io.StringIO(newline='')
    writer = csv.writer(stream, lineterminator='\n')
    writer.writerow(['cardRef', 'internalIndex', 'resourceId', 'name', 'properties', 'classCode', 'attack', 'defense', 'level', 'pack', 'Yugi', 'Kaiba', 'Joey', 'callbackLookup', *AXES])
    for c, a in zip(snapshot['cards'], auxiliary['cards']):
        writer.writerow([c['id'], *[c['identity'][k] for k in ('internalIndex', 'resourceId', 'name')],
            *[a[k] for k in ('properties', 'classCode', 'attack', 'defense', 'level', 'pack')],
            *[str(a['availability'][g]).lower() for g in ('Yugi', 'Kaiba', 'Joey')], c['callbackLookup']['state'],
            *[c['coverage'][axis]['state'] for axis in AXES]])
    return stream.getvalue().encode('utf-8')


def artifacts(snapshot, auxiliary, annotations=()):
    """Raw artifacts never depend on annotations; merged output has separate names."""
    outputs = {'inventory.json': canonical(snapshot), 'attributes.json': canonical(auxiliary),
               'inventory.csv': csv_bytes(snapshot, auxiliary)}
    if annotations:
        from scripts.coverage.annotations import merge_annotations
        registry = merge_annotations(snapshot, annotations, ROOT)
        outputs.update({'registry.json': canonical(registry), 'registry.csv': csv_bytes(registry, auxiliary)})
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dat', required=True, type=Path)
    parser.add_argument('--exe', required=True, type=Path, help='Read only, SHA256 verification; never executed')
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--standard-schema', action='store_true', help='Also validate with preinstalled jsonschema Draft202012Validator; never installs packages')
    parser.add_argument('--annotations', action='append', type=Path, default=[], help='Reviewed v1 overlay; repeatable, conflicts fail')
    args = parser.parse_args()
    if (ROOT / 'src/Joey.Core/Archive.cs').read_bytes() != blob('src/Joey.Core/Archive.cs') or (ROOT / 'src/Joey.Core/NativeAiAttributes.cs').read_bytes() != blob('src/Joey.Core/NativeAiAttributes.cs'):
        raise ValueError('Core source differs from pinned extraction baseline; review required')
    catalog = json.loads(blob(CALLBACK_PATH))
    if digest(args.exe.read_bytes()) != catalog['target_sha256']:
        raise ValueError('EXE fingerprint mismatch')
    extracted = json.loads(subprocess.check_output(['dotnet', 'run', '--no-build', '--project',
        str(ROOT / 'src/Joey.Coverage'), '--', str(args.dat)], cwd=ROOT))
    ledger = [evidence('helper', 'src/Joey.Core/NativeAiAttributes.cs', 'SourceInspection', 'Helper-only alias comparisons; original identities retained.'), evidence('callbacks', CALLBACK_PATH, 'StaticAnalysis', 'Closed callback table for the attested executable.'),
              evidence('extraction', 'src/Joey.Core/Archive.cs', 'SourceInspection', 'Catalog.Load identity and attribute decoding; input fingerprints recorded separately.'),
              evidence('pack', 'docs/24-dat-graphics.md', 'StaticAnalysis', 'card_pack bits 1/2/4 denote game availability, not full catalog.')]
    snapshot, auxiliary = generate(extracted, catalog, SOURCE_COMMIT, ledger)
    annotations = [json.loads(path.read_text(encoding='utf-8')) for path in args.annotations]
    outputs = artifacts(snapshot, auxiliary, annotations)
    if args.standard_schema:
        from jsonschema import Draft202012Validator
        Draft202012Validator.check_schema(SCHEMA)
        Draft202012Validator(SCHEMA).validate(snapshot)
        if 'registry.json' in outputs:
            Draft202012Validator(SCHEMA).validate(json.loads(outputs['registry.json']))
    args.output.mkdir(parents=True, exist_ok=True)
    for name, data in outputs.items():
        (args.output / name).write_bytes(data)
    print(canonical(auxiliary['summary']).decode(), end='')


if __name__ == '__main__':
    main()
