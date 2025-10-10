# SubtleByte Performance Template

This template project lives in `templates/SubtleByte.Template` and provides a clean, modular
starting point for rebuilding the SubtleByte mod with performance in mind. It keeps the
existing production code untouched so you can experiment on a fresh branch while still
referencing the old implementation.

## Key ideas

- **Server-only bootstrapping** – the plugin aborts immediately if it is accidentally
  loaded on a client build, preventing unnecessary allocations.
- **Module host** – features live in small `IModule` classes that can opt into the
  lightweight `IUpdateModule` interface. Modules are created lazily from factories,
  which keeps the template free from static singletons.
- **Interval scheduler** – recurring jobs use `IntervalScheduler` rather than running
  work inside `Update` every frame. This keeps the server’s hot loop clear.
- **Performance tracker** – the `PerformanceTracker` wraps module initialization and
  update calls and emits warnings whenever a module exceeds the configured budget
  (5 ms by default). You can tighten or relax the threshold per module by creating
  dedicated trackers.
- **Unity host behaviour** – a minimal `ServerBehaviour` component drives the module
  host. It is registered with `ClassInjector` so IL2CPP servers can instantiate it
  without additional boilerplate.

## Getting started on a new branch

1. Create a new branch off `main` (for example `rewrite/perf-template`).
2. Copy `templates/SubtleByte.Template` next to your new branch’s `VeinWares.SubtleByte`
   project folder (or repoint the solution to the template project directly).
3. Rename the namespace/package identifiers as needed (the template ships with
   `veinwares.subtlebyte.template`).
4. Add new modules in `templates/SubtleByte.Template/Modules`. Modules can request
   scheduled work via `ModuleContext.Scheduler.Schedule` and patch Harmony hooks via
   `ModuleContext.Harmony` (exposed as the Harmony **2.x** API under the
   `HarmonyLib` namespace – no separate `Lib.Harmony` reference is required).
   `ModuleContext.Harmony`.
5. Once satisfied with the new layout, replace the original project references in the
   `.sln` file or keep both projects side-by-side while you migrate functionality.

## Extending the template

- Implement `IDisposable` on modules to clean up Harmony patches or scheduled tasks.
- Use `ModuleContext.Performance.Measure` when wrapping bespoke operations that are
  not part of the module life-cycle.
- Create thin feature modules (e.g., `BottleRefundModule`) that inject their own
  configuration files instead of relying on global static configuration.

This structure should keep high-frequency tasks contained and observable while you
re-implement gameplay logic without inheriting the historical complexity that led to
server slowdowns. When you are ready to migrate real features, follow the staged
roadmap in [`docs/rewrite-roadmap.md`](./rewrite-roadmap.md) to keep the rewrite
focused on measurable performance wins.
server slowdowns.
