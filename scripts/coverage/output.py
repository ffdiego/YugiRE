"""Publish a known set of generated files; retire stale owned outputs only."""
from pathlib import Path
import os
import tempfile

REPORTS = {'coverage-report.md', 'investigation-priorities.md'}
GENERATED = REPORTS | {'inventory.json', 'attributes.json', 'inventory.csv', 'registry.json', 'registry.csv'}


def publish(directory, outputs, owned=GENERATED):
    directory = Path(directory)
    if not set(outputs) <= owned:
        raise ValueError('Unexpected generated filename')
    directory.mkdir(parents=True, exist_ok=True)
    staged = []
    try:
        for name, data in sorted(outputs.items()):
            with tempfile.NamedTemporaryFile(dir=directory, prefix='.coverage-', delete=False) as stream:
                staged.append((Path(stream.name), directory / name))
                stream.write(data)
        for temp, target in staged:
            os.replace(temp, target)
        for name in sorted(owned - set(outputs)):
            (directory / name).unlink(missing_ok=True)
    finally:
        for temp, _ in staged:
            temp.unlink(missing_ok=True)
