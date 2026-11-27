set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

capture = None
active_row = None
pending = None
entries = []

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little")

def thread():
    return gdb.selected_thread().num

class HardwareProbe(gdb.Breakpoint):
    def __init__(self,address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Impact(HardwareProbe):
    def stop(self):
        global capture
        sp=reg("esp")
        if capture or read(sp)!=0x00422e10 or read(sp+0x7c)!=0x00414777 or read(sp+12)!=224:
            return False
        dst,src=read(sp+4),read(sp+8)
        if read(dst+0x74)==3 and read(src+0x74)==10:
            capture={"thread":thread(),"sourceFormat":10,"destinationFormat":3,"intensity":224}
        return False

class Row(HardwareProbe):
    def stop(self):
        global active_row
        if capture is None or thread()!=capture["thread"]:
            return False
        sp=reg("esp")
        pick=read(sp+0x34)%7 == 0
        if pick:
            active_row={"x":reg("ecx"),"y":read(sp+0x34),"count":reg("eax"),"i":0}
        before.enabled = pick
        after.enabled = pick
        return False

class PixelBefore(HardwareProbe):
    def stop(self):
        global pending
        if capture is None or thread()!=capture["thread"]:
            return False
        sp=reg("esp")
        dst,src=read(sp),read(sp+4)
        if active_row["i"]%7==0:
            key=read(reg("ecx"),2)
            intensity=read(reg("ecx")+4)
            pending=(dst,{"x":active_row["x"]+active_row["i"],"y":active_row["y"],
                          "source":read(src),"key":key,"intensity":intensity,"before":read(dst,2)})
        return False

class PixelAfter(HardwareProbe):
    def stop(self):
        global pending
        if capture is None or thread()!=capture["thread"]:
            return False
        if pending:
            dst,row=pending
            row["after"]=read(dst,2)
            entries.append(row)
            pending=None
        active_row["i"]+=1
        if active_row["i"]==active_row["count"]:
            before.enabled=False
            after.enabled=False
        return len(entries)>=144

impact=Impact(0x00422e30)
row=Row(0x00423b99)
before=PixelBefore(0x00423bab)
after=PixelAfter(0x00423bb0)
before.enabled=False
after.enabled=False
end
continue
python
if capture and entries:
    print("attack-packed-pixels " + json.dumps({**capture,"samples":entries}))
end
detach
quit
