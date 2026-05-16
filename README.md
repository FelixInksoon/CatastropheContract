# Catastrophe Contract

This folder now contains a source-first mod scaffold for Slay the Spire 2.

## Layout

- `Catastrophe Contract.json`: mod manifest used by the loader.
- `src/`: C# source project for the gameplay logic and Harmony patches.
- `godot/`: Godot scenes and localization resources intended for the `.pck`.
- `tools/`: helper scripts for local build packaging.

## Current state

The implementation includes:

- Data-driven contract definitions and category metadata
- Persistent preset and best-risk record storage
- Runtime state container for selected contracts
- Harmony patch scaffolding targeting custom run, character select, run start, and run end flows
- A Godot scene and localization table for a dedicated Catastrophe Contract panel

The code is intentionally conservative where game internals are not yet verified from source. The remaining work is wiring the reflected menu nodes and validating the concrete method/property names in a live game session.

## Build intent

The intended outputs are:

- `Catastrophe Contract.dll`
- `Catastrophe Contract.pck`

The current machine does not have a .NET SDK or Godot export tooling installed, so this scaffold focuses on the source and packaging structure rather than producing final binaries in-place.
