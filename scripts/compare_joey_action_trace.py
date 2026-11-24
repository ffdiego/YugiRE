#!/usr/bin/env python3
"""Compare an explicit native command window with executed C# trace rows.

The projection is deliberately partial. Unmapped rows remain in the report;
a match with gaps is inconclusive, never evidence of full rule parity.
"""
from __future__ import annotations
import argparse
import json
from pathlib import Path
from inspect_joey_replay import read_file


def words(value, bits=16):
    if not isinstance(value,list) or len(value)!=4 or any(type(v) is not int or not 0<=v<(1<<bits) for v in value):
        raise ValueError("expected exactly four unsigned argument words")
    return value


def native_rows(path):
    if path.suffix.lower()==".ydr":
        records,_=read_file(path)
        return "ydr-record", [{"index":r["record_index"],"args":[int(r["raw_opcode_hex"],16),
            r["argument_1"],r["argument_2"],r["argument_3"]]} for r in records]
    if path.suffix.lower()!=".jsonl":
        raise ValueError("native input must be .ydr or .jsonl")
    result=[]
    metadata=None
    previous=None
    footer=False
    with path.open(encoding="utf-8") as f:
        for line in f:
            if not line.strip(): continue
            row=json.loads(line)
            if not isinstance(row,dict): raise ValueError("capture row must be object")
            kind=row.get("kind")
            if metadata is None:
                if kind!="metadata": raise ValueError("capture metadata required")
                metadata=row
                if row.get("overwritten",0)!=0: raise ValueError("capture has overwritten events")
                continue
            if footer: raise ValueError("capture content after footer")
            if kind=="capture_summary":
                if row.get("overwritten",0)!=0 or row.get("complete") is False:
                    raise ValueError("capture footer reports loss/incomplete run")
                footer=True
                continue
            seq=row.get("seq")
            if type(seq) is not int or seq<1 or (previous is not None and seq!=previous+1):
                raise ValueError("capture sequence gap")
            previous=seq
            if kind=="command":
                result.append({"index":len(result),"sequence":seq,"args":words(row.get("args"),32)})
    if metadata is None: raise ValueError("capture metadata required")
    return "producer-command-ordinal",result


def window(rows,start,count):
    if type(start) is not int or type(count) is not int or start<0 or count<1 or start+count>len(rows):
        raise ValueError("window outside input")
    return rows[start:start+count]


def canonical(args):
    # Keep full raw words; unknown high words or upper opcode flags are
    # intentionally not masked away for comparisons.
    args=words(args,32)
    if any(v>65535 for v in args) or args[0]&0x7000: return None
    raw,a,b,c=args
    op=raw&0xfff
    if op==0x61 and c==0 and (a,b) in ((0,5),(1,1)):
        return [[raw,1,1,0] for _ in range(b)]
    if op in (0x42,0x43) and a>0 and b==0 and c==1:
        return [args]
    return None


def csharp_rows(doc):
    if not isinstance(doc,dict) or doc.get("schema")!="yugire-action-projection-v1" or type(doc.get("complete")) is not bool:
        raise ValueError("unsupported C# trace schema")
    if not isinstance(doc.get("scope"),str) or not doc["scope"]:
        raise ValueError("trace scope required")
    rows=doc.get("rows")
    if not isinstance(rows,list): raise ValueError("C# trace rows required")
    previous=0
    for row in rows:
        if not isinstance(row,dict): raise ValueError("invalid trace row")
        seq=row.get("sequence")
        if type(seq) is not int or seq<=previous: raise ValueError("nonincreasing C# sequence")
        previous=seq
        mapping=row.get("mapping")
        if mapping not in ("mapped","unmapped","non_applied"): raise ValueError("unknown mapping status")
        if type(row.get("side")) is not int or row["side"] not in (0,1): raise ValueError("invalid C# side")
        if mapping=="mapped":
            if row.get("outcome")!="Applied": raise ValueError("nonapplied command has mapping")
            raw=words(row.get("args"))
            projected=canonical(raw)
            if projected!=[raw] or raw[0]>>15!=row["side"]:
                raise ValueError("invalid canonical C# arguments")
            expected={0x61:"DrawCommand",0x42:"HealCommand",0x43:"DamageCommand"}[raw[0]&0xfff]
            if row.get("command")!=expected: raise ValueError("C# command does not match its mapping")
        elif row.get("args") is not None: raise ValueError("unmapped command cannot carry fabricated words")
        elif mapping=="non_applied" and row.get("outcome") not in ("Rejected","Skipped"):
            raise ValueError("invalid nonapplied outcome")
        elif mapping=="unmapped" and row.get("outcome")!="Applied":
            raise ValueError("invalid unmapped outcome")
    return rows


def compare(native,doc,native_start,native_count,cs_start,cs_count,source_kind):
    left,right=window(native,native_start,native_count),window(csharp_rows(doc),cs_start,cs_count)
    l,r,gaps_l,gaps_r=[],[],[],[]
    for row in left:
        projected=canonical(row["args"])
        if projected is None: gaps_l.append(row)
        else:
            l.extend({"nativeIndex":row["index"],"nativeSequence":row.get("sequence"),"unit":i,"args":args}
                     for i,args in enumerate(projected))
    for row in right:
        if row["mapping"]!="mapped": gaps_r.append(row)
        else: r.append({"csharpSequence":row["sequence"],"args":row["args"]})
    differences=[]
    for i in range(max(len(l),len(r))):
        a=l[i] if i<len(l) else None
        b=r[i] if i<len(r) else None
        if a is None or b is None or a["args"]!=b["args"]:
            differences.append({"position":i,"native":a,"csharp":b})
    status="mismatch" if differences else ("inconclusive" if gaps_l or gaps_r or not l or not doc["complete"] else "projected_match")
    return {"schema":"yugire-action-comparison-v1","status":status,
            "scope":"ordered projection of ordinary draws and requested LP deltas; not rule parity",
            "nativeSourceKind":source_kind,"csharpScope":doc["scope"],"csharpComplete":doc["complete"],
            "nativeWindow":{"start":native_start,"count":native_count},
            "csharpWindow":{"start":cs_start,"count":cs_count},
            "nativeMappedUnits":len(l),"csharpMappedUnits":len(r),
            "nativeUnmapped":gaps_l,"csharpUnmapped":gaps_r,"differences":differences,
            "nativeProjected":l,"csharpProjected":r}


def main(argv=None):
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("native",type=Path);p.add_argument("csharp",type=Path)
    p.add_argument("--native-start",type=int,required=True);p.add_argument("--native-count",type=int,required=True)
    p.add_argument("--cs-start",type=int,required=True);p.add_argument("--cs-count",type=int,required=True)
    args=p.parse_args(argv)
    try:
        kind,rows=native_rows(args.native)
        doc=json.loads(args.csharp.read_text(encoding="utf-8"))
        report=compare(rows,doc,args.native_start,args.native_count,args.cs_start,args.cs_count,kind)
    except (OSError,ValueError,KeyError,TypeError,OverflowError) as exc:
        p.exit(2,f"action trace: {exc}\n")
    print(json.dumps(report,indent=2))
    return {"projected_match":0,"mismatch":1,"inconclusive":3}[report["status"]]


if __name__=="__main__": raise SystemExit(main())
