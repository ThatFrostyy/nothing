# Upgrade pickups and audio pool setup

## Hooking upgrade pickups into a scene
1. **Create/assign the prefab**
   - Add an `UpgradePickup` component to your pickup prefab.
   - Give it a `Collider2D` set to *Is Trigger*.
   - Assign the pickup's `SpriteRenderer` to `Indicator Renderer` and optionally add a child `Light2D` for glow.
   - Tune the visual fields (hover, rotation, pulse, shrink/fade, shake) if you want a different feel.
2. **Configure effects**
   - In the inspector, fill the `UpgradePickupSpawner.Available Effects` array with the Heal/Damage/Move Speed/Fire Rate entries you want to cycle through.
   - Set heal amounts, multipliers, durations, tint colors, and pickup sounds per entry.
3. **Place the spawner**
   - Drop an `UpgradePickupSpawner` in your scene and assign the `Pickup Prefab` plus the `Player` transform (or tag your player `Player`).
   - Adjust `Offscreen Padding`/`Min Spawn Radius` so spawns stay just off camera, and set `Lifetime Seconds` on the pickup to control auto-expiry.
   - The spawner auto-replaces expired/collected pickups after `Respawn Delay` seconds.

## Making pickups match the gun pickup animation
- The `UpgradePickup` script now uses the same hover, rotation, pulse, shrink, fade, and light fade behavior as `WeaponPickup`.
- To match visuals, use similar sprite/light settings as your gun pickup prefab and reuse its audio clip in the effect's `Pickup Sound` slot if desired.
- Camera shake on collection is enabled via the `Shake Duration`/`Shake Magnitude` fields.

## Audio pool usage
- Weapon fire now routes through `AudioPlaybackPool` automatically. No scene setup is required, but you can cap simultaneous sources at runtime via `AudioPlaybackPool.SetMaxSources(limit)` if you need stricter budgeting.
