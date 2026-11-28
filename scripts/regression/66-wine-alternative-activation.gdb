set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

activation = None
updates = []
draws = []
terminal = None

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4, signed=False):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little", signed=signed)

def thread():
    return gdb.selected_thread().num

class Probe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Construct(Probe):
    def stop(self):
        global activation
        if activation:
            return False
        caller = read(reg("esp"))
        if caller not in (0x00406e5e, 0x0040704d, 0x0040acd3):
            return False
        activation = {"thread":thread(),"object":reg("ecx"),"caller":caller}
        print(f"alternative-constructor caller={caller:#010x}", flush=True)
        return False

def is_current(obj):
    return activation and activation["object"] == obj and activation["thread"] == thread()

class Update(Probe):
    def stop(self):
        obj = reg("ecx")
        if is_current(obj):
            updates.append(read(obj + 0x10, signed=True))
        return False

class Draw(Probe):
    def stop(self):
        obj = reg("ecx")
        if is_current(obj):
            draws.append(read(obj + 0x10, signed=True))
        return False

class EndUpdate(Probe):
    def stop(self):
        global terminal
        obj = reg("esi")
        if is_current(obj) and not read(obj + 4, 1):
            terminal = read(obj + 0x10)
            return True
        return False

probes = [Construct(0x00450730), Update(0x00450a90), Draw(0x00450ad0), EndUpdate(0x00450abf)]
end
continue
python
if activation:
    print("alternative-activation " + json.dumps({"caller":activation["caller"],
          "updates":updates,"draws":draws,"terminal":terminal}))
end
detach
quit
