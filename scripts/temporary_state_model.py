"""Model active association entries in 0x56AEA0 and flag mask in 0x574230.

Static transcription only. Does not represent inactive tails retained by
0x5695E0, queue timing, or the event that schedules cleanup.
"""

def cleanup_associations(entries, occupied=True, category=0):
    if not occupied and category != 11:
        return list(entries)
    result = []
    for index, metadata in entries:
        kind = metadata & 15
        timer = (metadata >> 4) & 15
        if kind == 0 or (kind in (3, 12) and timer == 0):
            continue
        if kind in (3, 12):
            metadata = (metadata & 0xff0f) | ((timer - 1) << 4)
        result.append((index, metadata))
    return result


def cleanup_flags(value):
    return value & 0xffff
