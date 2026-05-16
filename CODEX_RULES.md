# CODEX_RULES

## Purpose

This file is the current handoff for future Codex/AI work on `CatastropheContract`.
It is not meant to replay the whole chat history. It should tell the next agent:

- what the mod currently is
- what has already been fixed
- what is stable
- what is only “implemented in code but not yet proven”
- what the current risky areas are
- what should be done next

When sources conflict, use this priority:

1. Current source code and current built DLL/PCK state
2. Latest user-validated conclusions from this thread
3. `record.md`
4. `README.md`

`README.md` is not current and must not be treated as the source of truth.

---

## Project Goal

- Mod name: `CatastropheContract`
- Target game: Slay the Spire 2
- Feature: add a “Catastrophe Contract” challenge system to `Custom Run / 自定义模式`
- Technical style: official mod loading + `BaseLib` + `Harmony` + heavy runtime reflection

Design constraints that remain true:

- Do not assume there is a stable official API for this feature.
- Do not silently clear vanilla custom modifiers or Ascension for the player.
- If a feature is not actually stable, mark it as not verified instead of pretending it is complete.
- Prefer preserving the user’s current working tree over “cleaning things up”.

---

## Important Files

### Runtime / gameplay

- `src/Content/ContractMutatorRegistry.cs`
  Central effect dispatcher. Most gameplay behavior lives here.
- `src/Content/ContractRuntimeReflection.cs`
  Reflection bridge for finding player/enemies/creatures/HP/powers/heal methods.
  This is still one of the highest-risk files.
- `src/Core/State/ContractStateStore.cs`
  Current run state and persistent selection/highest-risk save data.
- `src/Core/State/ContractCombatRuntimeState.cs`
  Per-combat runtime storage for custom enemy status values and damage dedupe.

### Harmony patches

- `src/Patches/CustomRunScreenPatch.cs`
  Injects contract UI into custom run screen.
- `src/Patches/CharacterSelectPatch.cs`
  Captures selected contracts before run start.
- `src/Patches/RunLifecyclePatch.cs`
  Hooks run/combat lifecycle and applies mutators.
- `src/Patches/CombatHookPatch.cs`
  Hooks combat damage/turn-related `Hook.*` methods.
- `src/Patches/HealingPatch.cs`
  Global `Heal` interception for `run_out`.

### UI / Godot

- `src/UI/ContractPanelNode.cs`
  Runtime C# UI class compiled into the main DLL.
- `godot/ui/CatastropheContractPanel.tscn`
  Scene used for panel injection.
- `godot/_generated/...`
  Godot generated script metadata. Must remain compiled into the DLL.

### Build / outputs

- `CatastropheContract.dll`
- `CatastropheContract.pck`
- `build-scriptmeta.rsp`
- `src/CatastropheContract.csproj`

---

## Current Runtime Truth

### Initialization

Initialization is intentionally redundant:

- `src/Bootstrap/ModuleInit.cs`
- `src/Bootstrap/ModEntry.cs`

Both paths may initialize the mod, but global locking was added to reduce duplicate patching behavior.

### Current UI loading path

The currently-correct direction is:

- custom run screen patch injects the Godot scene first
- scene path is `res://mods/CatastropheContract/godot/ui/CatastropheContractPanel.tscn`
- assembly name must stay `CatastropheContract`
- Godot generated script metadata must remain in the compiled DLL

This matters because earlier failures came from:

- wrong assembly name (`CatastropheContract.new`)
- missing Godot generated script metadata
- injecting an empty runtime-only UI shell instead of the scene-backed implementation

### Build identity

Current latest build marker in source:

- `2026-05-16-plating-bloodthirsty-v5`

Always ask the user to fully restart the game and confirm the build marker in logs when validating behavior.

---

## Major Fixes Already Completed

These were real issues during this thread and were addressed in code:

- UI not showing at all
  - fixed by restoring scene-based loading, correct assembly name, and Godot script metadata inclusion
- combat start freeze
  - fixed by reducing expensive recursive runtime scans and adding safer initialization/patch guards
- `activating`
  - fixed so enemy max HP increase also fully restores current HP
- player-target debuffs not applying
  - fixed by better player-to-creature resolution and delayed/retried application logic
- `burning`
  - corrected to use `Weak + Frail` and correct `2/4/6` values
- `erosion`
  - fixed and user-verified
- `burning`
  - fixed and user-verified
- `secret_battle`
  - fixed and user-verified
- `high_valued_object`
  - fixed and user-verified

---

## Current Status By Feature

### Verified working by user

- `erosion`
- `burning`
- `secret_battle`
- `high_valued_object`
- `activating`
- UI now appears
- combat no longer freezes on battle start

### Implemented in code, but should still be treated as needing verification

- `thorn`
- `great_awakening`
- `metallization`
- `industrialization`
- `economic_crisis`
- `run_out`
- `tightened_belt`
- `ultimate_defense`
- `countdown`

### In progress right now

- `debris_covered`
  - visible `PlatingPower` now appears
  - user reported it did not benefit the first turn correctly
  - latest code in `v5` now also grants immediate block on application
  - this latest adjustment is not yet user-verified

- `bloodthirsty`
  - older hidden runtime-only version was not acceptable
  - current direction is:
    - runtime custom status value on enemy
    - visible power layer via `MegaCrit.Sts2.Core.Models.Powers.RavenousPower`
    - actual healing handled by our own combat hook logic
  - user reported earlier versions had neither visible status nor actual lifesteal
  - latest `v5` expands damage-hook coverage and re-adds visible `RavenousPower`
  - this latest adjustment is not yet user-verified

### Still not implemented or not production-ready

- `swarming_elites`
- `congregating_bosses`
- `malaise`
- `restriction`
- `secret_action`
- `inefficiency`
- `antidetection`
- `linear_battlefield`
- `counterforce`

### Special note: Ascension / vanilla custom modifier conflict check

Code for run-start conflict blocking was added during this thread, but it was not conclusively user-verified as solved.
Treat this as:

- implemented attempt exists
- still needs real validation

Do not describe it as fully proven.

---

## Current Technical Notes

### Player/enemy resolution

The runtime often exposes:

- `Player`
- wrapper objects
- underlying `Creature`

Most gameplay effects only work reliably after resolving down to the actual creature object.
If a future change breaks effects again, inspect:

- `TryGetPlayerFromCandidates`
- `TryGetCreature`
- mutation target enumeration

### Healing / no-heal

`run_out` depends on `HealingPatch`, which scans all `Heal` methods in `sts2` and rolls player HP back if healing occurred.
This is powerful but broad and fragile.
Be cautious when changing:

- `TryHeal`
- `HealingPatch`
- global hook coverage

### Power application

`ContractRuntimeReflection.TryApplyPower(...)` is central to visible status behavior.
It currently supports async-returning `PowerCmd.Apply(...)` calls.

Important real observations from logs:

- `PlatingPower` applies through `PowerCmd.Apply(...)`
- `RavenousPower` has previously appeared successfully on enemies in older builds
- `CoveredPower` was wrong for current `debris_covered` expectations because the user explicitly wants `Plating`, not `Covered`

### Combat hook coverage

Current damage-related coverage includes:

- `AfterDamageGiven`
- `AfterDamageReceived`
- `AfterCurrentHpChanged`
- `AfterModifyingHpLostAfterOsty`

This was expanded because earlier lifesteal logic missed real player HP loss events.

### Duplicate patch risk

Earlier in this thread, duplicate patching happened because extra DLLs/backups in the mod directory could get loaded.
Global locking was added, but it is still safer to avoid leaving stray alternate mod DLLs in the live mod folder.

---

## Current Known Pitfalls

### 1. UI and Godot binding are sensitive to assembly identity

Do not casually change:

- assembly name
- Godot generated file inclusion
- scene script path assumptions

If you break any of those, the UI can “load” but instantiate no real script.

### 2. `ContractRuntimeReflection.cs` is powerful but brittle

Small reflection changes can fix one contract and silently break several others.
After any meaningful reflection change, regression-test at least:

- `erosion`
- `activating`
- `burning`
- `secret_battle`
- `high_valued_object`
- `run_out`

### 3. `debris_covered` and `bloodthirsty` are the current hotspot

These two are the most actively changing features right now.
Do not mark either one as “done” without explicit user confirmation.

### 4. `README.md` is stale

Ignore old statements about missing SDK/toolchain support.
The project is already actively building DLL/PCK outputs.

### 5. Logs matter

The user often validates through:

- `C:\Users\91395\AppData\Roaming\SlayTheSpire2\logs\godot.log`
- `E:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\mods\CatastropheContract\debug.log`
- manually extracted tails like `D:\临时\后两百行.txt`

When debugging, always key off exact build markers and exact latest log segments.

---

## What Happened In This Thread

High-level chronology:

1. Read project rules and source.
2. Fixed missing player-side contract effects:
   - `erosion`
   - `burning`
   - `secret_battle`
   - `high_valued_object`
3. Fixed `activating` so enemy current HP is also restored.
4. Broke UI temporarily while moving runtime/UI loading logic.
5. Restored UI by reverting to scene-based loading, correcting assembly name, and compiling Godot generated metadata.
6. Introduced a combat-start freeze while widening runtime scans.
7. Fixed the freeze by narrowing reflection traversal and reducing duplicate-patch hazards.
8. Implemented first hidden-runtime versions of `debris_covered` and `bloodthirsty`.
9. User rejected hidden-only behavior because:
   - `debris_covered` should be real visible plating
   - `bloodthirsty` should behave like a real status/effect
10. Switched `debris_covered` toward visible `PlatingPower`.
11. Reintroduced visible `RavenousPower` as a display layer for `bloodthirsty`.
12. Expanded combat damage hook coverage for lifesteal.
13. Latest unresolved user feedback:
   - plating visible, but not helping from turn one as expected
   - bloodthirsty still lacked visible display and real effect
14. Latest code (`v5`) addresses both of those, but is not yet confirmed by the user.

---

## Current Next Steps

Priority order from the live state of this thread:

1. Re-test `debris_covered`
   - confirm visible plating appears
   - confirm first-turn benefit is now correct
2. Re-test `bloodthirsty`
   - confirm visible `RavenousPower` layer appears on enemies
   - confirm actual healing happens after enemy deals real HP loss
3. Only after those two are solid:
   - prepare/implement `ultimate_defense`
   - prepare/implement `countdown`
   - prepare/implement `counterforce`

Do not move on to claiming those later special-rule contracts are complete before the current two are closed.

---

## Validation Rules For Future Work

After changing gameplay code, prefer this validation checklist:

- fully exit the game
- restart the game
- start a fresh custom run
- do not rely on continuing an old save
- confirm latest build marker in logs
- confirm exact contract list in logs

For `debris_covered`, check:

- visible `PlatingPower` on enemy
- first-turn effect
- stack loss behavior after HP damage if fallback runtime logic is still in play

For `bloodthirsty`, check:

- visible status exists on enemy
- damage hook fires on real player HP loss
- heal amount matches contract percentage

If logs and user-visible behavior conflict, believe the user-visible behavior first and use logs only to explain why.

---

## File Naming Note

The repository currently contains `CODEX_RULES.md`.
If a future instruction says `CODEX_RULE.md`, treat it as referring to this file unless the user explicitly asks for a second separate rule file.
