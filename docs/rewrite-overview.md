# SubtleByte Rewrite Overview

The `VeinWares.SubtleByte.Rewrite` project is the performance-focused implementation that
rebuilds SubtleByte features on top of the modular host introduced by the template. The
rewrite currently ships with two modules:

- `HeartbeatModule` – emits a lightweight debug pulse every 30 seconds so operators can
  confirm that the module host is alive without keeping logic on the hot update loop.
- `BottleRefundModule` – ports the empty-bottle refund behaviour onto the modular stack
  with improved caching, entity pruning, and performance instrumentation. The module wires
  itself into `BloodMixerSystem_Update.OnUpdate` via Harmony, tracks mixer state
  transitions, and only touches inventories when a craft completes.

## Configuration

The rewrite exposes a single config entry at the moment:

| Section        | Key      | Default | Description |
|----------------|----------|---------|-------------|
| Bottle Refund  | Enabled  | `true`  | Toggle the automatic refund of one Empty Bottle when a blood mix finishes. |

All configuration entries live in the standard BepInEx config file for the rewrite plugin
(`veinwares.subtlebyte.rewrite.cfg`).

## Extending the rewrite

- Register new modules in `Plugin.Load` by adding additional factories to the
  `ModuleHost.Create` call.
- Use `ModuleContext.Performance.Measure` when wrapping long-running Harmony callbacks to
  keep visibility on spikes.
- Prefer caching entity lookups (as demonstrated in `BottleRefundModule`) to avoid
  repeatedly scanning the ECS world every frame.

This document will grow as more functionality from the legacy mod is migrated onto the
rewrite foundation.
