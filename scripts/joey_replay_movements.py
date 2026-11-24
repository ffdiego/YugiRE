"""Native occurrence and movement contracts (YDR high-mode version 1300).

A checkpoint context is an explicit observation at the start of a slice,
not inferred by skipping unknown records. It only models main-deck keys.
Projection is a lab edit, not a new rule decision or a full state replay.
"""
from __future__ import annotations
from dataclasses import dataclass
from joey_replay_placements import decode_placement


@dataclass(frozen=True)
class Movement:
    opcode: int
    source_side: int
    source_region: int
    source_index: int | None
    key: int | None
    destination_side: int | None
    destination_region: int
    destination_argument: int
    flags: int


def decode_movement(record: dict) -> Movement:
    raw=int(record["raw_opcode_hex"],16)
    a,b,c=(record[f"argument_{i}"] for i in (1,2,3))
    if raw & 0x7000:
        raise ValueError("unmapped movement upper flags")
    op=raw&0xfff
    if op==0x8e:
        if raw!=0x8e:
            raise ValueError("unmapped 8E opcode variant")
        source,region,key=a&1,(a>>1)&0x7f,a>>8
        dest,dregion,arg=b&1,(b>>1)&0x7f,b>>8
        if key<2 or region>15 or dregion>15:
            raise ValueError("invalid or unmapped 8E key/region")
        return Movement(op,source,region,None,key,dest,dregion,arg,c)
    if op==0x79:
        # 00404B40 uses region+index for field access, but list
        # region/index are distinct; word2==0x10 selects banished.
        # Preserve other raw variants for diagnostics, never project them.
        return Movement(op,raw>>15,a>>8,a&0xff,None,None,15 if b==0x10 else 14,b,c)
    raise ValueError("not a 79/8E movement")


def main_key_inventory(summary: dict) -> dict[int,int]:
    h=summary["header"]["header_fields_uninterpreted"]
    if h["0x0C"]!=1300 or h["0x10"] not in (0,1) or h["0x14"] not in (0,1):
        raise ValueError("native key reconstruction requires version 1300 and known selectors")
    entries=summary["deck_groups"]
    if len(entries)!=2:
        raise ValueError("expected two serialized decks")
    result={}
    for side,entry in enumerate(entries):
        ids=entry["groups"][0]["resource_ids"]
        if entry["side_index"]!=side or not 5<=len(ids)<=80 or any(
            type(v) is not int or not 0<v<=0xfff for v in ids):
            raise ValueError("invalid main deck inventory")
        result.update({2*(i+1)+side:v for i,v in enumerate(ids)})
    return result


_FIXED={"monster":5,"spell":5,"field":1}
_LISTS=("hand","deck","grave","banished")
_ZONES=(*_LISTS,*_FIXED)


def region_position(region: int, index: int) -> tuple[str,int]:
    if region in (11,13,14,15):
        if not 0<=index<160:
            raise ValueError("invalid native list index")
        return {11:"hand",13:"deck",14:"grave",15:"banished"}[region],index
    if 0<=region<11 and 0<=region+index<11:
        slot=region+index
        if slot<5: return "monster",slot
        if slot<10: return "spell",slot-5
        return "field",0
    raise ValueError("unmapped region/index (fusion or temporary object)")


class MovementContext:
    """Mutable key ledger for a contiguous supported projection.

    Each side supplies all seven zone arrays, fixed slots use null.
    Keys are the original native main-deck ordinals, not Joey.Core IDs.
    Missing cards outside these zones are not invented; all references
    must resolve. A full native snapshot and dynamic validation are pending.
    """
    def __init__(self,summary:dict,context:dict,start:int):
        self.inventory=main_key_inventory(summary)
        if not isinstance(context,dict) or context.get("record_index")!=start:
            raise ValueError("movement context must identify the first record of the slice")
        sides=context.get("sides")
        if not isinstance(sides,dict) or set(sides)!={"0","1"}:
            raise ValueError("context requires both sides")
        self.zones={}
        seen=set()
        for side in (0,1):
            entry=sides[str(side)]
            if not isinstance(entry,dict) or set(entry)!=set(_ZONES):
                raise ValueError("context must supply all seven supported zone arrays")
            for zone in _ZONES:
                values=entry[zone]
                if not isinstance(values,list) or (zone in _FIXED and len(values)!=_FIXED[zone]):
                    raise ValueError("invalid zone layout")
                if zone in _LISTS and len(values)>(80 if zone in ("hand","deck") else 160):
                    raise ValueError("zone exceeds laboratory bounds")
                for key in values:
                    if key is None and zone in _FIXED: continue
                    if type(key) is not int or key not in self.inventory or key in seen:
                        raise ValueError("unknown or duplicate native key in context")
                    seen.add(key)
                self.zones[side,zone]=list(values)

    def _at(self,side,zone,index):
        cards=self.zones[side,zone]
        if not 0<=index<len(cards) or cards[index] is None:
            raise ValueError("movement source missing from context")
        return cards[index]

    def _remove(self,side,zone,index):
        if zone in _FIXED: self.zones[side,zone][index]=None
        else: self.zones[side,zone].pop(index)

    def _append(self,side,zone,key):
        cards=self.zones[side,zone]
        if len(cards)>=(80 if zone in ("hand","deck") else 160):
            raise ValueError("movement destination full")
        cards.append(key)

    def apply_other(self,record):
        raw=int(record["raw_opcode_hex"],16)
        op,side=raw&0xfff,raw>>15
        if op==0x61:
            count=record["argument_2"]
            if len(self.zones[side,"deck"])<count or len(self.zones[side,"hand"])+count>80:
                raise ValueError("draw exceeds supplied context")
            keys=self.zones[side,"deck"][:count]
            del self.zones[side,"deck"][:count]
            self.zones[side,"hand"].extend(keys)
        elif op in (0xce,0xcf):
            p=decode_placement(record)
            key=self._at(p.side,"hand",p.hand_index)
            if self.inventory[key]!=p.resource_id or self.zones[p.side,p.zone][p.slot] is not None:
                raise ValueError("placement conflicts with movement context")
            self._remove(p.side,"hand",p.hand_index)
            self.zones[p.side,p.zone][p.slot]=key

    def project(self,record):
        m=decode_movement(record)
        side=m.source_side
        if m.opcode==0x8e:
            # Ordinary same-owner list transfers only. Destination hand
            # appends (ignores encoded index); grave/banish use owner.
            if m.flags not in (0,1) or m.source_region not in (11,13,14,15) or m.destination_region not in (11,14,15):
                raise ValueError("unsupported 8E flags or zones")
            zone,_=region_position(m.source_region,0)
            keys=self.zones[side,zone]
            if m.key not in keys:
                raise ValueError("8E native key is not in the recorded source zone")
            index=keys.index(m.key)
            key=m.key
            if m.destination_side!=side or key&1!=side:
                raise ValueError("cross-owner/controller 8E not supported")
            dest,_=region_position(m.destination_region,0)
        else:
            if m.flags not in (2,3) or m.destination_argument not in (1,0x10):
                raise ValueError("unsupported 79 flags or destination variant")
            if m.source_region not in (0,11):
                raise ValueError("unsupported 79 region (temporary/offset field variant)")
            zone,index=region_position(m.source_region,m.source_index)
            if zone not in ("monster","spell","hand"):
                raise ValueError("unsupported 79 source zone")
            key=self._at(side,zone,index)
            if key&1!=side:
                raise ValueError("79 destination belongs to a different owner")
            dest="banished" if m.destination_region==15 else "grave"
        if zone==dest:
            raise ValueError("same-zone transfer not supported")
        resource=self.inventory[key]
        if 0x780<=resource<=0x783:
            raise ValueError("token removal not supported")
        self._append(side,dest,key)
        self._remove(side,zone,index)
        return f"move-id {resource} {side} {zone} {index} {dest}"
