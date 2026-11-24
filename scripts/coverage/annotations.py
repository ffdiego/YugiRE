"""Strict, offline semantic overlays. No automatic baseline migration or promotion."""
import copy
import hashlib
import json
import subprocess

from scripts.coverage.validation import SCHEMA, check_shape, validate

FIELDS = {'annotationVersion', 'baselineCommit', 'fingerprints', 'cards', 'mechanics', 'evidence'}
CARD_FIELDS = {'cardRef', 'identity', 'coverage', 'mechanicInventory', 'mechanics', 'openQuestions'}


def normalize(value, key=''):
    """Only set-valued lists are sorted; evidence event sequences stay untouched."""
    if isinstance(value, dict):
        return {k: normalize(v, k) for k, v in value.items()}
    if isinstance(value, list):
        result = [normalize(v) for v in value]
        if key.endswith('Refs') or key == 'dependencies':
            return sorted(result)
        if key == 'mechanics':
            return sorted(result, key=lambda v: v.get('id', v.get('mechanicRef')))
        return result
    return value


def verify_evidence(entry, root):
    loc = entry['location']
    path = loc['path']
    if path.startswith('/') or ':' in path or '\\' in path or any(p in ('', '.', '..') for p in path.split('/')):
        raise ValueError('Evidence path must be repository-relative')
    try:
        data = subprocess.check_output(['git', 'show', f"{loc['commit']}:{path}"], cwd=root, stderr=subprocess.PIPE)
    except subprocess.CalledProcessError as exc:
        raise ValueError('Pinned evidence blob unavailable: ' + entry['id']) from exc
    if hashlib.sha256(data).hexdigest() != loc['sha256']:
        raise ValueError('Evidence hash mismatch: ' + entry['id'])


def merge_annotations(inventory, documents, root):
    """Return a new registry. Identical definitions coalesce; conflicts always fail."""
    validate(inventory)
    result = copy.deepcopy(inventory)
    cards = {c['id']: c for c in result['cards']}
    tables = {key: {v['id']: v for v in result[key]} for key in ('mechanics', 'evidence')}
    patches = {}
    for doc in documents:
        if not isinstance(doc, dict) or set(doc) != FIELDS:
            raise ValueError('Unexpected or missing annotation field')
        if type(doc['annotationVersion']) is not int or doc['annotationVersion'] != 1:
            raise ValueError('Unsupported annotation version')
        if doc['baselineCommit'] != inventory['baselineCommit'] or doc['fingerprints'] != inventory['fingerprints']:
            raise ValueError('Annotation baseline/fingerprints mismatch; explicit review required')
        for key in ('cards', 'mechanics', 'evidence'):
            if not isinstance(doc[key], list):
                raise ValueError('Annotation collection must be an array')
            seen = set()
            for original in doc[key]:
                row = normalize(original)
                if key == 'cards':
                    if not isinstance(row, dict) or set(row) != CARD_FIELDS:
                        raise ValueError('Unexpected or missing annotated card field')
                    id = row['cardRef']
                    if id not in cards or row['identity'] != cards[id]['identity']:
                        raise ValueError('Annotated card identity mismatch')
                    # Reuse v1 definitions; callback lookup remains exclusively extracted.
                    candidate = dict(cards[id], **{k: v for k, v in row.items() if k not in ('cardRef', 'identity')})
                    check_shape(candidate, SCHEMA['$defs']['card'])
                    target = patches
                else:
                    check_shape(row, SCHEMA['$defs']['mechanic' if key == 'mechanics' else 'evidence'])
                    id = row['id']
                    target = tables[key]
                if id in seen:
                    raise ValueError('Duplicate annotation definition: ' + id)
                seen.add(id)
                if id in target and normalize(target[id]) != row:
                    raise ValueError('Conflicting annotation definition: ' + id)
                target[id] = row
    for id, patch in patches.items():
        cards[id].update({k: v for k, v in patch.items() if k not in ('cardRef', 'identity')})
    for key, table in tables.items():
        result[key] = sorted(table.values(), key=lambda v: v['id'])
    result['cards'] = sorted(cards.values(), key=lambda c: c['identity']['internalIndex'])
    result['callbacks'].sort(key=lambda c: c['row'])
    result['aliases'].sort(key=lambda a: a['id'])
    result = normalize(result)
    validate(result)
    for entry in result['evidence']:
        verify_evidence(entry, root)
    return result
