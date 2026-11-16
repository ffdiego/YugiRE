"""Validate supplied original files offline; output technical metadata only."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
from joey_visual import Archive, inventory, sha256_file, yga, scene_records

EXE_SHA256 = 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de'
DAT_SHA256 = '7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('dat', type=Path)
    parser.add_argument('--exe', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    before = {'exe': sha256_file(args.exe), 'dat': sha256_file(args.dat)}
    if before != {'exe': EXE_SHA256, 'dat': DAT_SHA256}:
        raise ValueError('Original artifact fingerprint mismatch')
    archive = Archive(args.dat)
    report = inventory(archive, decode_yga=True)
    expected = {'.bin': 41, '.bmp': 2763, '.txt': 167, '.sue': 2, '.yga': 537, '.ydc': 58}
    if report['extensions'] != expected:
        raise ValueError('Corpus inventory mismatch')
    samples = []
    names = [r'cursor\cursor_e_summon.yga', r'j\duel\effect\coin_sha.yga',
             r'j\duel\effect\dice_shadow.yga', r'j\duel\effect\vija_smoke.yga',
             r'j\duel\effect\kouka_ue_b.yga', r'j\duel\effect\expl1.yga',
             r'j\title\mask.yga']
    for name in names:
        entry = next(e for e in archive.entries if e.name == name)
        data = archive.read(entry)
        info, pixels = yga(data)
        samples.append(dict(name=name, dat_offset=entry.offset, resource_size=len(data),
                            resource_sha256=hashlib.sha256(data).hexdigest(), **info))
    strip_checks = []
    for scene, resource, count in [
        (r'j\duel\effect\coin.txt', 'coin_sha.yga', 4),
        (r'j\duel\effect\coin.txt', 'coin_kirari.yga', 4),
        (r'j\duel\effect\dice.txt', 'dice_shadow.yga', 6),
        (r'j\duel\effect\vija.txt', 'vija_smoke.yga', 5),
        (r'j\duel\effect\vija.txt', 'vija_moji_smo.yga', 8),
    ]:
        record = next(r for r in scene_records(archive.read(scene)) if r['resource'] == resource)
        declared = record['parameter'] // 1000 if scene.endswith('vija.txt') else record['parameter']
        if declared != count:
            raise ValueError('Unexpected manifest division parameter')
        # Resource paths use Windows separators, regardless of host.
        name = scene.rsplit('\\', 1)[0] + '\\' + resource
        info, _ = yga(archive.read(name))
        if info['width'] % count:
            raise ValueError('Manifest strip does not divide raster')
        strip_checks.append(dict(manifest=scene, resource=resource, parameter=record['parameter'],
                                 divisions=count, frame_width=info['width'] // count,
                                 frame_height=info['height']))
    after = {'exe': sha256_file(args.exe), 'dat': sha256_file(args.dat)}
    if after != before:
        raise ValueError('Artifact changed during validation')
    result = dict(evidence_type='offline resource validation; static loader corroboration documented separately',
                  native_execution=False, before=before, after=after, count=report['count'],
                  extensions=report['extensions'], yga_decoded=537,
                  yga_modes=dict(Counter(str(r['yga']['mode']) for r in report['resources'] if 'yga' in r)),
                  bmp_depths=dict(Counter(str(r['bitmap']['bits']) for r in report['resources'] if 'bitmap' in r)),
                  samples=samples, strip_checks=strip_checks)
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(dict(resources=report['count'], yga_decoded=537, manifest_strip_checks=len(strip_checks), artifacts_unchanged=True)))

if __name__ == '__main__':
    main()
