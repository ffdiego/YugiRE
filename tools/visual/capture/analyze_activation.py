"""Validate a bounded observer JSONL; preserve measured QPC times without inventing FPS."""
import argparse
from collections import Counter
import json
from pathlib import Path
import statistics
from capture_activation import EXE_SHA, DAT_SHA


def analyze(rows):
    if len(rows) < 4 or rows[0].get('kind') != 'metadata' or rows[-1].get('kind') != 'footer':
        raise ValueError('Missing metadata/footer')
    meta, footer = rows[0], rows[-1]
    if meta.get('schema') != 'joey-activation-v1' or meta.get('exeSha256') != EXE_SHA or meta.get('nativeExecution') is not True or meta.get('hostAttestedDatSha256') != DAT_SHA:
        raise ValueError('Missing Windows capture provenance')
    if (footer.get('finished') is not True or footer.get('detached') is not True or
            footer.get('exeUnchanged') is not True or footer.get('datUnchanged') is not True or footer.get('overflow') is not False or
            footer.get('readErrors') != 0 or footer.get('hostErrors') != 0):
        raise ValueError('Incomplete/error/overflow capture')
    events = rows[1:-1]
    if footer.get('emitted') != len(events) or [r.get('seq') for r in events] != list(range(1,len(events)+1)):
        raise ValueError('Lost, repeated or out-of-order events')
    allowed={'armed','start','update_enter','update_leave','draw_enter','draw_leave','transform','polygon','blit','complete'}
    if not events or events[0]['kind']!='armed' or any(r['kind'] not in allowed for r in events):
        raise ValueError('Unknown event type or missing arm event')
    ticks = [int(r['ticks']) for r in events]
    if any(a>b for a,b in zip(ticks,ticks[1:])):
        raise ValueError('Nonmonotonic clock')
    starts = [r for r in events if r['kind']=='start']
    completions = [r for r in events if r['kind']=='complete']
    if len(starts)!=1 or len(completions)!=1:
        raise ValueError('Expected exactly one effect')
    start=starts[0]
    frequency=int(start['qpcFrequency'])
    table=start['cosineTable']
    if str(frequency)!=events[0].get('qpcFrequency'):
        raise ValueError('Clock frequency changed')
    if frequency<=0 or len(table)!=512 or any(type(v) is not int or abs(v)>65536 for v in table):
        raise ValueError('Invalid QPC frequency/cosine table')
    initial=start['state']
    object_id=initial['object']
    if initial['counter'] != 0 or initial['monster'] != 0:
        raise ValueError('Capture did not start at zero for blue activation')
    enters=[r for r in events if r['kind']=='update_enter']
    leaves=[r for r in events if r['kind']=='update_leave']
    if [r['state']['counter'] for r in enters]!=list(range(0,257,16)) or [r['state']['counter'] for r in leaves]!=list(range(16,273,16)):
        raise ValueError('Incomplete or unexpected update counter sequence')
    if completions[0]['state']['counter']!=272 or completions[0]['state']['active']!=0:
        raise ValueError('Unexpected terminal state')
    frames=[]
    drawing=None
    pending_update=None
    for row in events:
        state=row.get('state')
        if state is not None and state['object']!=object_id:
            raise ValueError('Mixed effect objects')
        if row['kind']=='update_enter':
            if pending_update is not None or drawing is not None:
                raise ValueError('Nested update or update during selected draw')
            pending_update=row
        elif row['kind']=='update_leave':
            if pending_update is None or row['thread']!=pending_update['thread'] or row['state']['counter']!=pending_update['state']['counter']+16:
                raise ValueError('Unpaired/cross-thread update')
            pending_update=None
        elif row['kind']=='draw_enter':
            if drawing is not None: raise ValueError('Nested draw')
            drawing=dict(state=row['state'], thread=row['thread'], ticks=int(row['ticks']), transforms=[], polygons=[], blits=[], operationOrder=[])
        elif row['kind'] in ('transform','polygon','blit'):
            if drawing is None or drawing['thread']!=row['thread']:
                raise ValueError('Orphan/cross-thread draw operation')
            drawing[{'transform':'transforms','polygon':'polygons','blit':'blits'}[row['kind']]].append(row)
            drawing['operationOrder'].append(row['kind'])
        elif row['kind']=='draw_leave':
            if drawing is None or drawing['thread']!=row['thread']:
                raise ValueError('Unpaired draw')
            state=drawing['state']; counter=state['counter']
            if not 0<=counter<=256 or counter%16 or state['monster']!=0:
                raise ValueError('Unsupported draw counter/category')
            transforms=drawing['transforms']
            scale=table[(counter-128)&511]; opacity=min(scale>>8,255)
            if len(transforms)!=2:
                raise ValueError('Expected two transformed YGAs')
            for index, op in enumerate(transforms):
                args=op['args']; src=op['source']; dst=op['destination']
                if args[2:] != [state['x'],state['y'],counter//2 if index==0 else counter,scale,scale,opacity]:
                    raise ValueError('Captured transform differs from static contract')
                if (src['width'],src['height'],src['colorKey'],src['alphaFlag'])!=(164,164,0,1):
                    raise ValueError('Unexpected source geometry/color key/alpha flag')
                if (dst['format'],src['format']) not in ((7,12),(8,13)):
                    raise ValueError('Unsupported display/source pixel format; 16-bit requires separate validation')
            if len(drawing['blits'])!=1:
                raise ValueError('Expected middle scratch-surface blit')
            expected_order=['transform','blit','transform'] if scale==0 else ['transform','polygon','blit','transform','polygon']
            if drawing['operationOrder']!=expected_order:
                raise ValueError('Unexpected pass order')
            blit=drawing['blits'][0]
            if (blit['source']['width'],blit['source']['height'],blit['source']['colorKey'],blit['source']['alphaFlag']) != (state['cardWidth'],state['cardHeight'],0xffffff,0):
                raise ValueError('Unexpected scratch surface')
            if blit['args'][1:4] != [state['x']-state['cardWidth']//2,state['y']-state['cardHeight']//2,scale>>9]:
                raise ValueError('Unexpected scratch placement/opacity')
            expected_polygons=0 if scale==0 else 2
            if len(drawing['polygons'])!=expected_polygons:
                raise ValueError('Missing transformed polygon events')
            for polygon in drawing['polygons']:
                if polygon['uv'] != [0,0,163,0,163,163,0,163] or polygon['opacity'] != opacity:
                    raise ValueError('Unsupported polygon UV/opacity')
            frames.append(dict(counter=counter,x=state['x'],y=state['y'],cardWidth=state['cardWidth'],
                               cardHeight=state['cardHeight'],seconds=(drawing['ticks']-int(start['ticks']))/frequency,
                               polygons=[dict(xy=p['xy'],uv=p['uv'],opacity=p['opacity']) for p in drawing['polygons']]))
            drawing=None
    if drawing is not None or pending_update is not None or not frames:
        raise ValueError('Unfinished/absent draw observations')
    intervals=[(int(b['ticks'])-int(a['ticks']))/frequency for a,b in zip(enters,enters[1:])]
    return dict(schema='joey-activation-replay-v1',exeSha256=EXE_SHA,datSha256=DAT_SHA,
                evidence='structurally validated instrumented Windows capture; pixel fidelity not yet established',
                nativeExecutionDeclared=True,qpcFrequency=frequency,cosineTable=table,frames=frames,
                updates=len(enters),draws=len(frames),drawCounterHistogram=dict(Counter(f['counter'] for f in frames)),
                updateIntervalsSeconds=intervals,
                medianUpdateIntervalSeconds=statistics.median(intervals),
                minUpdateIntervalSeconds=min(intervals),maxUpdateIntervalSeconds=max(intervals))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture',type=Path)
    parser.add_argument('--output',required=True,type=Path)
    args=parser.parse_args()
    if args.capture.stat().st_size>8*1024*1024: raise ValueError('Capture exceeds bound')
    rows=[json.loads(line) for line in args.capture.read_text().splitlines() if line.strip()]
    result=analyze(rows)
    args.output.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps({k:result[k] for k in ['updates','draws','medianUpdateIntervalSeconds','minUpdateIntervalSeconds','maxUpdateIntervalSeconds']}))

if __name__=='__main__': main()
