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

Additional seasonal, elite, and visual buff options are available in the same section for
fine-grained control of late-game ambushes.
