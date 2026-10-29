"""Inspect four bounded card resources; preserve payloads in memory only.
Run the lifecycle validator first to verify original archive fingerprints.
"""
from pathlib import Path
import struct,hashlib,json
repo=Path(__file__).resolve().parent.parent; archive=repo.parent/'passion/data.dat'
def decode(src,size):
    ring=bytearray(4096); pos=0xfee; flags=0; i=0; out=bytearray()
    while len(out)<size:
        flags >>=1
        if not flags&256: flags=src[i]|0xff00; i+=1
        if flags&1: seq=[src[i]]; i+=1
        else:
            a,b=src[i:i+2]; i+=2; start=a|((b&240)<<4); seq=None
        if seq is not None:
            v=seq[0];out.append(v);ring[pos]=v;pos=(pos+1)&4095
        else:
            for n in range((b&15)+3):
                v=ring[(start+n)&4095];out.append(v);ring[pos]=v;pos=(pos+1)&4095
                if len(out)==size: break
    assert i==len(src)
    return bytes(out)
with archive.open('rb') as f:
    assert f.read(8)==b'KCEJYUGI'; count=struct.unpack('<I',f.read(4))[0]; rows=f.read(count*268)
    def resource(name,expected=None):
        for i in range(count):
            row=rows[i*268:(i+1)*268]; n=bytes((v>>4)|((v&15)<<4) for v in row[:256]).split(b'\0')[0].decode('ascii')
            if n.lower()==name:
                offset,size,stored=struct.unpack_from('<III',row,256); f.seek(offset); src=f.read(stored); data=src if size==stored else decode(src,size); assert expected is None or hashlib.sha256(data).hexdigest()==expected
                return data
        raise ValueError(name)
    props=resource('bin#\\card_prop.bin','1a772642a04d86a2cfcebba2d0f4ff40a1737761296d0ea6a2645f5ed2bf0b9e')
    names=resource('bin#\\card_nameeng.bin','74848d0b2cd7d5fac7a767075bd5b01e0366183ce934afa6070ce24394a24032')
    desc=resource('bin#\\card_desceng.bin','751d81fe6e8484afc53a311e8fc1c66b5fc50a5b1f4a95edba241422b3c27a92')
    offsets=resource('bin#\\card_indxeng.bin','e4918a06fa86882e8998220f067ed36fd5ca8c148c62f6a1bedd15b48e11ce60')
    description_offset=struct.unpack_from('<I',offsets,197*4)[0]
    description=desc[description_offset:].split(b'\0')[0].decode('ascii')
    assert description.startswith('FLIP:') and '1000' in description
    sample=[]
    for i in range(1115):
        word=struct.unpack_from('<I',props,i*4)[0]
        if ((word>>20)&31)==10 and len(sample)<20:
            sample.append(dict(index=i,name=names[i*64:(i+1)*64].split(b'\0')[0].decode('ascii'),class_code=10))
    result=dict(method='Four bounded resources decoded in memory; all output SHA256 checked. Properties/names hashes from earlier rounds; description/offsets pinned in this round.',samples=sample,description=description,description_offset=description_offset,description_sha256=hashlib.sha256(desc).hexdigest(),offsets_sha256=hashlib.sha256(offsets).hexdigest(),parasite_property_word=hex(struct.unpack_from('<I',props,197*4)[0]))
    (repo/'docs/evidence/parasite-class10-samples.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
