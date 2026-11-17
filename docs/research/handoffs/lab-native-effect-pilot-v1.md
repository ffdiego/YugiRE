# Handoff — P045 mixed-deck pilot (15/11/2026)

Branch: `feature/effect-lab-pilot-v1`, dependent on PR #4
(`feature/headless-duel-sandbox-v1`). Do not merge PR #5 before #4.

## Objetivo

Run 10 sandbox duels with normal monsters and already-audited partial P045
fixed-damage spells; then perform a **three-duel native observation pilot**
with the same *card identities*. Use existing tested YugiLab command capture;
do not reactivate PR #1 experimental internal hooks.

## Code changes

- `scripts/prepare_effect_pilot.py`: imports locally reviewed C# normal pool,
  generated coverage registry + attributes. Checks fingerprints, identity,
  Joey pack bit, class/category and explicit review. Exports two same-index
  native deck JSONs, a C# mixed sandbox manifest and provenance plan.
- `yugilab/effect_report.py`: reads native JSONL/directory offline, checks
  contiguous seq/footer and reports CF/CE-associated P045 resource candidates
  and LP commands separately by duel. **Not effect activation evidence.**
- `tests/test_effect_pilot.py` and `yugilab/tests/test_effect_report.py`: portable
  synthetic tests for identity guards, deterministic decks, trace integrity
  and source-call-site conservative filtering.
- `docs/research/native-effect-pilot-v1.md`: exact Windows runbook.

## Validation and stop gate

Source code and API contracts inspected against existing GitHub repo.
**Python tests have not been executed in this environment** (there is no
repository checkout plus generated data/validated native binary here).
Run the two targeted unittest commands from the runbook, then prepare decks.
Stop immediately on identity mismatch, native crash, capture loss, checksum
mismatch, or abnormal process cleanup. Native 3-duel capture is operator-only.

## Facts versus interpretation

- P045 source identity pairs in generator are derived from previous audited
  coverage; resource/pack identity is rechecked against the generated registry.
- `CF` and `CE` resource fields are *candidates* based on known callers.
  A nearby `0x43` command is not proof that the card produced LP damage.
- Native result, winner, card effect application, decision equivalence and
  Core comparison remain unverified until a specific action is causally
  observed and state context is complete.

Next: Windows local tests, ten mixed C# duels, three native duels, command
audit. If useful CF/CE candidates appear, investigate one Sparks trace
with passive state observation and an independent differential fixture.
