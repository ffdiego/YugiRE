"""Static transcription of 0x599570 and numeric portion of 0x599140.

No game execution. Caller eligibility, list construction, overflow and UI are
outside this model. See docs/cards/ritual-bindings.md.
"""

def subset_search(levels, remaining, minimum):
    """Transcribe native reverse-index recursion, including its signed bounds."""
    def visit(last, required, limit):
        for index in range(last, -1, -1):
            level = levels[index]
            if level < required:
                if index and visit(index - 1, required - level, min(limit, level)):
                    return True
            elif level < required + limit:
                return True
        return False
    return visit(len(levels) - 1, remaining, minimum)


def candidate_can_finish(candidate, others, remaining, minimum=255):
    """Numeric checks only; assumes the native caller has accepted the card."""
    if remaining <= candidate < remaining + minimum:
        return True
    return subset_search(others, remaining - candidate, min(minimum, candidate))


def progress(remaining, minimum, selected):
    """Observed update in 0x5995E0 / 0x5999D0, on valid byte-sized levels."""
    return max(0, remaining - selected), min(minimum, selected)
