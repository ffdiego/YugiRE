#!/usr/bin/env python3
"""Record root drawable DamageNotify events from an isolated X display."""

import argparse
import ctypes as C
import json
import select
import time

XID = C.c_ulong
Display = C.c_void_p
X11 = C.CDLL("libX11.so.6")
Xdamage = C.CDLL("libXdamage.so.1")
RAW_RECTANGLES = 0


class Rectangle(C.Structure):
    _fields_ = [
        ("x", C.c_short), ("y", C.c_short),
        ("width", C.c_ushort), ("height", C.c_ushort),
    ]

    def coordinates(self):
        return [self.x, self.y, self.width, self.height]


class DamageNotify(C.Structure):
    _fields_ = [
        ("type", C.c_int), ("serial", C.c_ulong), ("send_event", C.c_int),
        ("display", Display), ("drawable", XID), ("damage", XID),
        ("level", C.c_int), ("more", C.c_int), ("timestamp", C.c_ulong),
        ("area", Rectangle), ("geometry", Rectangle),
    ]


class Event(C.Union):
    _fields_ = [("damage", DamageNotify), ("padding", C.c_long * 24)]


def bind(library, name, result, *arguments):
    function = getattr(library, name)
    function.restype = result
    function.argtypes = arguments
    return function


open_display = bind(X11, "XOpenDisplay", Display, C.c_char_p)
close_display = bind(X11, "XCloseDisplay", C.c_int, Display)
root_window = bind(X11, "XDefaultRootWindow", XID, Display)
connection_number = bind(X11, "XConnectionNumber", C.c_int, Display)
sync = bind(X11, "XSync", C.c_int, Display, C.c_int)
pending = bind(X11, "XPending", C.c_int, Display)
next_event = bind(X11, "XNextEvent", C.c_int, Display, C.POINTER(Event))
query_extension = bind(Xdamage, "XDamageQueryExtension", C.c_int, Display,
                       C.POINTER(C.c_int), C.POINTER(C.c_int))
query_version = bind(Xdamage, "XDamageQueryVersion", C.c_int, Display,
                     C.POINTER(C.c_int), C.POINTER(C.c_int))
create_damage = bind(Xdamage, "XDamageCreate", XID, Display, XID, C.c_int)
destroy_damage = bind(Xdamage, "XDamageDestroy", None, Display, XID)


def observe(display, seconds):
    event_base, error_base = C.c_int(), C.c_int()
    major, minor = C.c_int(), C.c_int()
    if not query_extension(display, C.byref(event_base), C.byref(error_base)):
        raise RuntimeError("X server does not advertise DAMAGE")
    if not query_version(display, C.byref(major), C.byref(minor)):
        raise RuntimeError("Could not negotiate DAMAGE")
    root = root_window(display)
    damage = create_damage(display, root, RAW_RECTANGLES)
    try:
        sync(display, 0)
        event = Event()
        while pending(display):
            next_event(display, C.byref(event))
        started = time.monotonic_ns()
        deadline = started + seconds * 1_000_000_000
        fd = connection_number(display)
        events = []
        while (remaining := deadline - time.monotonic_ns()) > 0:
            if not pending(display) and not select.select([fd], [], [], remaining / 1e9)[0]:
                break
            if not pending(display):
                continue
            next_event(display, C.byref(event))
            item = event.damage
            if item.type == event_base.value and item.damage == damage:
                events.append({
                    "serverMs": item.timestamp,
                    "receivedNs": time.monotonic_ns() - started,
                    "area": item.area.coordinates(),
                    "geometry": item.geometry.coordinates(),
                    "more": bool(item.more),
                })
        return {"version": [major.value, minor.value], "seconds": seconds, "events": events}
    finally:
        destroy_damage(display, damage)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--display", required=True, help="dedicated X display, e.g. :91")
    parser.add_argument("--seconds", type=int, default=10)
    args = parser.parse_args()
    if not 1 <= args.seconds <= 60:
        parser.error("--seconds must be between 1 and 60")
    display = open_display(args.display.encode())
    if not display:
        parser.error("cannot open the selected display, check DISPLAY and XAUTHORITY")
    try:
        print(json.dumps(observe(display, args.seconds)))
    finally:
        close_display(display)


if __name__ == "__main__":
    main()
