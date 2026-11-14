#!/usr/bin/env python3
"""Generate deterministic Markdown from existing registry and attribute JSON."""
import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from scripts.coverage.reports import coverage_report, priorities_report
from scripts.coverage.annotations import verify_evidence
from scripts.coverage.output import publish, REPORTS


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--registry', required=True, type=Path)
    p.add_argument('--attributes', required=True, type=Path)
    p.add_argument('--output', required=True, type=Path)
    p.add_argument('--coverage-report', action='store_true')
    p.add_argument('--investigation-priorities', action='store_true')
    args = p.parse_args()
    if not (args.coverage_report or args.investigation_priorities):
        p.error('Select at least one report')
    registry = json.loads(args.registry.read_text(encoding='utf-8'))
    attributes = json.loads(args.attributes.read_text(encoding='utf-8'))
    outputs = {}
    if args.coverage_report:
        outputs['coverage-report.md'] = coverage_report(registry, attributes)
    if args.investigation_priorities:
        outputs['investigation-priorities.md'] = priorities_report(registry, attributes)
    for entry in registry['evidence']:
        verify_evidence(entry, ROOT)
    publish(args.output, outputs, REPORTS)


if __name__ == '__main__':
    main()
