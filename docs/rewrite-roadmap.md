# SubtleByte rewrite – next steps

This checklist turns the performance template into a practical replacement for the existing SubtleByte mod.  Each phase builds on the previous one so you can benchmark the server while porting features in controlled batches.

## 1. Establish the baseline
- **Clone the template onto a fresh branch.** Use `rewrite/perf-template` (or similar) and copy `templates/SubtleByte.Template` beside the live `VeinWares.SubtleByte` project so both can be built together.
- **Wire the template into the solution.** Replace the old project reference or temporarily add a new solution configuration that points the dedicated server to the template plugin.
- **Run the server with *no gameplay modules enabled*.** Capture boot logs and frame timings; this defines the clean-room baseline you can compare against the legacy build.

## 2. Port shared infrastructure
- **Configuration files.** Move only the schema types that multiple systems require (e.g., prestige config, bottle refund options) into a dedicated `Config` module with input validation.
- **Data access helpers.** Re-implement inventory, blood, or recipe helpers as thin services that return immutable DTOs.  Guard every method with `PerformanceTracker.Measure` blocks so heavy calls are visible early.
- **Logging and telemetry.** Decide whether to keep the existing logging wrappers or rely on Unity’s logger + RCON echo.  Whatever you choose, wrap it in a module so it can be toggled in benchmarks.

## 3. Rebuild prestige without inherited defects
- **Deterministic modification IDs.** Pre-compute the ID per stat (`ModificationId.FromRaw((ushort)id)`) instead of calling `NewId` each refresh.
- **Component-preserving buffs.** Copy the original prefab once, add your stat modifiers, and avoid stripping ability-related components so persistence stays intact.
- **Stat clamps and validation.** Clamp health, attack speed, and other replicated values before writing to ECS buffers.  Log (once) when a clamp occurs so misconfigured data can be fixed without flooding the console.
- **Integration tests.** Use the scheduler to simulate a prestige level-up every few seconds while bots connect/disconnect; verify the server does not emit `Synced` or component-missing warnings.

## 4. Reintroduce auxiliary systems (bottle refund, QoL modules)
- **One module per feature.** Keep bottle refunds isolated from prestige.  Modules should expose a `Configure(ModuleContext context)` method where Harmony patches and scheduler jobs are registered.
- **Inventory safety checks.** Ensure every inventory mutation verifies stack counts, ownership, and locking before applying changes.  Return early if the conditions are not met.
- **Performance gates.** Wrap any per-craft loops with `PerformanceTracker.Measure("BottleRefund")` so refunds cannot regress server frame times unnoticed.

## 5. Validation and rollout
- **Load test.** Spin up a dedicated server with bots (or recorded player sessions) to confirm CPU usage remains stable and logs are quiet.
- **Persistence migration.** Build a one-off cleanup script that removes the legacy prestige buffs and re-seeds the new deterministic versions.
- **Deployment checklist.** Document branch/tag versions, required config files, and rollback steps so the live server can swap to the rewritten mod safely.

Following this roadmap keeps the rewrite focused on measurable improvements, reduces the risk of reintroducing the old replication issues, and provides clear checkpoints where you can stop and evaluate performance before adding more complexity.
