import copy
import math
from pathlib import Path
import sys
import unittest
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'capture'))
from analyze_activation import analyze
from capture_activation import EXE_SHA, DAT_SHA


def synthetic_rows():
    """Synthetic timing/operations for parser rejection tests, never a native oracle."""
    rows=[dict(kind='metadata',schema='joey-activation-v1',nativeExecution=True,exeSha256=EXE_SHA,hostAttestedDatSha256=DAT_SHA)]
    tick=1000
    def add(kind,**fields):
        nonlocal tick
        tick+=10
        rows.append(dict(kind=kind,seq=len(rows),ticks=str(tick),thread=1,**fields))
    def state(counter):
        return dict(object='0x1000',counter=counter,active=int(counter<272),monster=0,x=525,y=352,cardWidth=54,cardHeight=78)
    table=[math.trunc(math.cos(i*math.pi/256)*65536) for i in range(512)]
    add('armed',qpcFrequency='1000')
    add('start',state=state(0),qpcFrequency='1000',cosineTable=table)
    for before in range(0,257,16):
        tick=2000+before*10
        add('update_enter',state=state(before));add('update_leave',state=state(before+16))
        if before==256: break
        counter=before+16;scale=table[(counter-128)&511];opacity=min(scale>>8,255)
        add('draw_enter',state=state(counter))
        for index in range(2):
            src=dict(width=164,height=164,colorKey=0,alphaFlag=1,format=12)
            add('transform',args=[0,1,525,352,counter//2 if index==0 else counter,scale,scale,opacity],source=src,destination=dict(format=7))
            if scale:
                add('polygon',xy=[0]*8,uv=[0,0,163,0,163,163,0,163],opacity=opacity)
            if index==0:
                add('blit',args=[0,498,313,scale>>9,0,0,0],source=dict(width=54,height=78,colorKey=0xffffff,alphaFlag=0))
        add('draw_leave',state=state(counter))
    add('complete',state=state(272))
    rows.append(dict(kind='footer',finished=True,detached=True,exeUnchanged=True,datUnchanged=True,overflow=False,readErrors=0,hostErrors=0,emitted=len(rows)-1))
    return rows


class CaptureTests(unittest.TestCase):
    def test_measured_intervals_and_draws(self):
        result=analyze(synthetic_rows())
        self.assertEqual((result['updates'],result['draws']),(17,16))
        self.assertAlmostEqual(result['medianUpdateIntervalSeconds'],0.16)
        self.assertEqual(result['frames'][0]['counter'],16)

    def test_incomplete_overflow_errors_and_fingerprint_fail_closed(self):
        for key,value in [('finished',False),('detached',False),('exeUnchanged',False),('datUnchanged',False),('overflow',True),('readErrors',1),('hostErrors',1)]:
            rows=synthetic_rows();rows[-1][key]=value
            with self.subTest(key=key),self.assertRaises(ValueError):analyze(rows)
        for key,value in [('nativeExecution',False),('exeSha256','0'*64),('schema','unknown')]:
            rows=synthetic_rows();rows[0][key]=value
            with self.assertRaises(ValueError):analyze(rows)

    def test_missing_and_reordered_events(self):
        rows=synthetic_rows();rows.pop(3)
        with self.assertRaises(ValueError):analyze(rows)
        rows=synthetic_rows();rows[2]['seq']=1
        with self.assertRaises(ValueError):analyze(rows)
        rows=synthetic_rows();rows[3]['ticks']='1'
        with self.assertRaises(ValueError):analyze(rows)

    def test_wrong_transform_format_and_thread(self):
        for change in ['angle','format','thread','scratch','counter']:
            rows=synthetic_rows()
            op=next(r for r in rows if r['kind']=='transform')
            if change=='angle':op['args'][4]+=1
            if change=='format':op['source']['format']=11
            if change=='thread':op['thread']=2
            if change=='scratch':next(r for r in rows if r['kind']=='blit')['source']['width']=0
            if change=='counter':next(r for r in rows if r['kind']=='update_leave')['state']['counter']=0
            with self.subTest(change=change),self.assertRaises(ValueError):analyze(rows)

    def test_missing_polygon_and_unknown_clock(self):
        rows=synthetic_rows();next(r for r in rows if r['kind']=='polygon')['kind']='unknown'
        with self.assertRaises(ValueError):analyze(rows)
        rows=synthetic_rows();next(r for r in rows if r['kind']=='start')['qpcFrequency']='0'
        with self.assertRaises(ValueError):analyze(rows)

if __name__=='__main__':unittest.main()
