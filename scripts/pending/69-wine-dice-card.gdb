set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

command = None
update_ticks = []
draw_ticks = []

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4, signed=False):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little", signed=signed)

class Probe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Enqueue(Probe):
    def stop(self):
        global command
        sp = reg("esp")
        args = [read(sp + i * 4) for i in range(4)]
        if not command and (args[0] & 0xfff) in (0xe2, 0xe3):
            command = {"args": args, "thread": gdb.selected_thread().num}
            print("dice-queued " + json.dumps(args), flush=True)
        return False

def belongs(obj):
    return (command is not None and command["thread"] == gdb.selected_thread().num
            and read(obj) == 0x005db3f4)

class Update(Probe):
    def stop(self):
        obj = reg("ecx")
        if belongs(obj):
            if not update_ticks:
                command["index"] = read(obj + 0x14)
                command["center"] = [read(obj + offset, signed=True) for offset in (0x18, 0x1c)]
            update_ticks.append(read(obj + 0x10, signed=True))
        return False

class Draw(Probe):
    def stop(self):
        obj = reg("ecx")
        if belongs(obj):
            draw_ticks.append(read(obj + 0x10, signed=True))
        return False

class EndUpdate(Probe):
    def stop(self):
        obj = reg("ecx")
        if not belongs(obj) or read(obj + 4, 1):
            return False
        command["terminal"] = read(obj + 0x10, signed=True)
        return True

probes = [Enqueue(0x0058caf1), Update(0x00456b70), Draw(0x00456b90), EndUpdate(0x00456b84)]
end
continue
python
if command and "terminal" in command:
    del command["thread"]
    print("dice-card " + json.dumps({**command, "updates": update_ticks, "draws": draw_ticks}))
end
detach
quit
