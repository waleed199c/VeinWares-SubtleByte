# Faction Infamy configuration

The SubtleByte configuration file (`Genji.VeinWares-SubtleByte.cfg`) exposes a set of
options under the **Faction Infamy** section that shape ambush behaviour. The table
below highlights the core values that server operators tend to tune most often.

| Setting | Default | Description |
| --- | --- | --- |
| `Hate Gain Multiplier` | `1.0` | Scales how much hate each qualifying kill grants. Lower values slow ambush progression, while higher values accelerate it. |
| `Minimum Ambush Hate` | `50.0` | Minimum post-multiplier hate required before an ambush squad can spawn against a player. |
| `Ambush Chance Percent` | `50` | Roll that determines whether an eligible combat encounter actually spawns an ambush squad. |
| `DisableBloodConsumeOnSpawn` | `false` | When set to `true`, freshly spawned ambush units have their blood consume and feeding components removed so they cannot be fed upon immediately. |
| `DisableCharmOnSpawn` | `false` | When enabled, ambush squads remove charm sources and related debuffs so newly spawned enemies cannot be charmed right away. |

Additional seasonal, elite, and visual buff options are available in the same section for
fine-grained control of late-game ambushes.

## Elite ambush visuals

`EnableEliteAmbush` must stay enabled for any elite-only functionality, including the
`EnableAmbushVisualBuffs` toggle. When both options are true, elite ambush squads gain the
distinct "elite" visual effects while regular ambushes remain unchanged. Disabling either
setting prevents the additional visuals from appearing.
