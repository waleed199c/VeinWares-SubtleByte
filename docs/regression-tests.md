# Regression tests

## Ambush scarecrow allegiance
1. Enable Halloween ambushes in the SubtleByte configuration and restart the server so the changes apply.
2. Use the faction infamy admin commands (e.g. `infamy.addhate <steamId> bandits 5000`) to push a test character into the elite ambush tier.
3. Summon the ambush squad (`infamy.forceambush <steamId> bandits`) and wait for the scarecrows to spawn alongside the faction's core units.
4. Confirm that the scarecrows do not attack or aggro their allied ambush squad members for at least 30 seconds of combat.
5. Repeat the check for a second faction (for example Militia) to ensure the faction-to-team mapping works for multiple ambush definitions.
