# Elite ambush stat scaling options

The Faction Infamy configuration now exposes more granular controls for elite ambush stat tuning.

## Squad-wide multipliers

The existing `Elite <Stat> Multiplier` entries continue to scale every member of an elite ambush squad whenever `Enable Elite Ambush` is true. These values default to `1.0` so existing configurations behave exactly as before.

## Representative bonuses

Each stat also offers representative-specific tuning that is only applied to squad representatives:

| Stat | Ratio entry | Additive entry | Default |
| ---- | ----------- | -------------- | ------- |
| Health | `Elite Representative Health Ratio` | `Elite Representative Health Bonus` | `1.0` / `0.0` |
| Damage reduction | `Elite Representative Damage Reduction Ratio` | `Elite Representative Damage Reduction Bonus` | `1.0` / `0.0` |
| Resistances | `Elite Representative Resistance Ratio` | `Elite Representative Resistance Bonus` | `1.0` / `0.0` |
| Power | `Elite Representative Power Ratio` | `Elite Representative Power Bonus` | `1.0` / `0.0` |
| Attack speed | `Elite Representative Attack Speed Ratio` | `Elite Representative Attack Speed Bonus` | `1.0` / `0.0` |
| Spell speed | `Elite Representative Spell Speed Ratio` | `Elite Representative Spell Speed Bonus` | `1.0` / `0.0` |
| Move speed | `Elite Representative Move Speed Ratio` | `Elite Representative Move Speed Bonus` | `1.0` / `0.0` |
| Knockback resistance | `Elite Representative Knockback Resistance Ratio` | `Elite Representative Knockback Resistance Bonus` | `1.0` / `0.0` |

The representative multiplier is calculated as:

```
finalMultiplier = squadMultiplier * representativeRatio + representativeBonus
```

This happens only when elite ambushes are enabled. Non-representative squad members continue to use the squad multiplier alone.
