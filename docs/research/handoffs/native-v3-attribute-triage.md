# V3 attribute triage — offline and conservative

2026-11-13. Branch: research/esi-crash. Captured source file (private):
commands-20261113-042853-907607-pid19584.jsonl.
SHA256: c7a8b618418061f68665c7c3e59574371a2061e23eb1d55965ac456fa0aaf80a.
Do not commit or redistribute the raw JSONL.

## Findings from the real v3 capture

The 50 F40 contexts have 50 pairs of native wrappers. Of these,
**29** F40 entries have linkCountRaw, monsterFlagsRaw and playerFlagsRaw
all equal to zero for BOTH sides, at the instant of the F40 snapshot.
That is a neutral-looking LOCAL snapshot, NOT proof that there are no
intrinsic, equipment, field, global or temporary effects.

Among those 29: 15 defenders are hidden, so ADDC0 returns a
heuristic estimate; 14 defenders are revealed. Only two of
the revealed defender cases use the already-supported plain monster,
Gemini Elf, internal index 550 (F40 entry sequences 70 and 74).
Neither Axe Raider (index 24) nor Gemini Elf (550) is an
attacker in this capture.

The Gemini Elf native wrapper buffer in both cases is
260212056C07000084030000 (index 550, type 18, attribute 5,
ATK 1900, DEF 900). This agrees with the existing closed
NativeAiAttributes plain-card baseline UNDER A DECLARED
NO-MODIFIER HYPOTHESIS. It does not constitute independently
observed effect eligibility or a complete F40 context.

## Implementation

- src/Joey.Lab/CombatAttributeTriage.cs adds a strict, read-only
  triage over existing CombatContextReader and CaptureAnalyzer.
- New Joey.Lab command: combat-attribute-triage CAPTURE with
  optional --dat PATH, --expected-sha256 HASH, --json.
- The DAT is SHA256 pinned to
  7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed.
  Card resource identity and base stats come from the DAT, not from native
  computed buffer data. Without DAT, no independent stats are computed.
- Core's NativeAiAttributes.FromPlainCard is invoked only for its
  closed Axe Raider/Gemini Elf subset, and only as a CONDITIONAL
  zero-modifier scenario. Never infer the modifier manifest
  from zeros in the v3 snapshot or the expected native output.
- Hidden defenders, unimplemented intrinsic policies, local link/flag
  activity and missing catalog are classified separately.
- The v3 parser rejects corrupt counters, wrong EXE, broken F40
  correlation and unpreserved wrapper ESI/ESP. Unsupported v2 data
  does not silently convert to v3.
- ValidatedIndependentContexts remains ZERO until consumed fields,
  effect manifests and other F40 predicates can be proved.
- Synthetic regression tests exist in
  src/tests/Joey.Tests/CombatAttributeTriageChecks.cs and are wired
  into Joey.Tests Program.cs.

## Windows run

From Z:\YugiRE\YugiRE-native-win:

    git pull --ff-only
    dotnet run --project .\src\tests\Joey.Tests -- "Z:\YugiRE\passion\data.dat"

Offline triage without DAT:

    dotnet run --project .\src\Joey.Lab -- combat-attribute-triage "Z:\YugiRE\YugiRE-native-win\scratch\traces\commands-20261113-042853-907607-pid19584.jsonl" --expected-sha256 c7a8b618418061f68665c7c3e59574371a2061e23eb1d55965ac456fa0aaf80a

Triage with the pinned original DAT:

    dotnet run --project .\src\Joey.Lab -- combat-attribute-triage "Z:\YugiRE\YugiRE-native-win\scratch\traces\commands-20261113-042853-907607-pid19584.jsonl" --dat "Z:\YugiRE\passion\data.dat" --expected-sha256 c7a8b618418061f68665c7c3e59574371a2061e23eb1d55965ac456fa0aaf80a

The expected coverage without DAT is 50 F40 frames / 29 neutral
candidates / 15 hidden defenders / 14 visible defenders, and
zero comparisons. With pinned DAT, the two Gemini Elf entries
are expected to produce CONDITIONAL matches, pending real .NET test.
Use --json for detailed rows. Do not publish raw capture.

## Limitations and next research gate

No validated independent F40 Match/Mismatch exists. Real modifier
manifest, consumed flags/position/seed, special intrinsic card policies,
and hidden-opponent estimates remain separate research questions.
Obtain catalog mapping for indices in the 29 candidates, inspect
intrinsic policy before extending Core support, and require a
closed modifier manifest before promoting conditional results.

The Python inspection of the provided JSONL confirmed all counts
in this handoff. The C# source and tests were authored remotely,
but could not run under the actual .NET SDK in this environment
(dotnet unavailable). Windows execution and tests are the next gate.
Main is unchanged; no new game instrumentation was performed.

## Windows verification and next catalog inspection

User executed the real Windows .NET 10 `Joey.Tests` suite against
`Z:\YugiRE\passion\data.dat`: **675 checks passed**, after the
raw-string fixture was replaced with static JSON plus typed `JsonNode`
mutations in commit `c9826c5`. This is a genuine Windows test result
reported by the operator, not a test run on the assistant's host.

The operator also executed `combat-attribute-triage` with the pinned
original DAT. Output verified: 50 F40 frames, 29 neutral-looking
snapshots, 14 revealed defenders, 15 hidden defender estimates, 2
conditional matches (Gemini Elf at seq 70/74), 0 conditional
mismatches and 0 fully independent contexts.

### Next four attacker candidates (from the existing private v3 JSONL)

| Internal index | Local-neutral frames | Native observed own ATK / DEF | Interpretation |
| --- | ---: | ---: | --- |
| 733 | 8 | 2400 / 2000 | native wrapper output only |
| 261 | 6 | 1100 / 1200 | native wrapper output only |
| 453 | 5 | 1000 / 100 | native wrapper output only |
| 364 | 3 | 2000 / 1600 | native wrapper output only |

**No resource IDs/card names are inferred from the numeric wrapper
outputs.** The input JSONL snapshots contain internal indexes but
`resourceId=null`; identification must come from the SHA256-pinned
`data.dat`. A new compact index summary in Joey.Lab prints the
`resource`, `name`, `count`, role and one native buffer per
distinct index when `--dat` is provided. This summary enhancement
was committed **after** the operator's 675-check run, so must be
compiled/verified in the next Windows checkout.

The current label `UnsupportedIntrinsicPolicy` means only that
`FromPlainCard` does not implement the identity's intrinsic-stat
policy **in its closed two-resource subset**. It does not by itself
establish that any such card has a dynamic effect or that its
base ATK/DEF cannot eventually be modeled independently.
