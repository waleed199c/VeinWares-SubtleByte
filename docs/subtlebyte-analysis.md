# SubtleByte update investigation

## Summary of observed server behaviour
- Unity reports missing components when loading persistence for prefab `-113436752` (`Snapshot_AbilityStateBuffer`, `AbilityJewelTemplate`, `AbilityCastCondition`, `BlobAssetOwner`, `AbilityGroupInfo`).
- After players connect the server logs a large number of `Synced Float` / `Synced Integer` out-of-range warnings for health, attack speed, and ability slot indices.
- The log also shows "JobTempAlloc" warnings, indicating that at least one system is allocating temporary native memory for longer than Unity's four-frame limit.

## Root cause analysis

### Prestige buff reapplication
`PrestigeMini.ApplyLevel` re-applies a hijacked buff every time a player connects or their prestige file changes. Each call replaces the buff's `ModifyUnitStatBuff_DOTS` buffer with one entry per configured stat, using `ModificationId.NewId(0)` for every line.【F:VeinWares.SubtleByte/Services/PrestigeMini.cs†L41-L138】 The underlying `ModificationId` domain is globally incremented; once it exceeds 511 the resulting IDs are outside the replication range (0–511), leading to the ability slot warnings seen in the log.

### Removal of ability-related components
When the mod applies "permanent" buffs it strips several components from the live buff entity, including `ReplaceAbilityOnSlotBuff` and other ability/gameplay event components.【F:VeinWares.SubtleByte/Utilities/Buffs.cs†L34-L64】 If the original prefab expected these components, the server's persistence loader can no longer find them on the prefab entity, producing the repeated "Component ... is not found" errors when the save is loaded.

### Lack of stat clamping
SubtleByte's prestige configuration allows arbitrary additive and multiplicative bonuses with no server-side clamp.【F:VeinWares.SubtleByte/Config/SubtleBytePrestigeConfig.cs†L108-L158】 Large values (for example, health > 100k or attack speed multipliers > 4×) push synced attributes beyond the engine's allowed range, triggering the `Synced Float ... out of range` warnings.

### Why the bottle-refund patch is not the culprit
The newly-added blood homogenizer hook only inspects inventories and optionally adds a single Empty Bottle item when it detects a craft transition from `Mixing` to `NotReadyToMix`. The code never touches ability components, persistence, or player stats—it interacts solely with inventory buffers and cached `ItemData` structs.【F:VeinWares.SubtleByte/Patches/BloodMixerSystemsPatch.cs†L16-L196】 Because of that, it cannot emit the ability-related warnings that the server logs, and it does not allocate native memory that survives past the end of the frame (all temporary arrays and maps are disposed immediately). The warnings therefore have to come from the prestige systems above rather than the bottle refund logic.

## Impact
- The flood of replication warnings increases log volume and wastes CPU while the game engine repeatedly clamps invalid values.
- Missing prefab components can prevent buffs/abilities from functioning correctly and may corrupt persistence data over time.

## Recommendations
1. Use deterministic `ModificationId` values (for example, reuse the same ID per stat) instead of continually calling `ModificationId.NewId(0)` every time the buff is refreshed.
2. Avoid removing ability-related components from buff prefabs unless the replacement logic supplies an alternative; otherwise, guard the removal behind configuration so the original prefab structure is preserved.
3. Introduce clamping/validation on prestige stat values before writing them into the buff to keep them within the engine's supported ranges (e.g., cap health ≤ 100,000 and attack speed multipliers ≤ 4).
4. After applying these fixes, clear existing prestige buffs so the live entities pick up the corrected component layout and stat IDs.
