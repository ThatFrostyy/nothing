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

## Main menu and scene switching setup
- A barebones `Assets/Scenes/MainMenu.unity` scene has been added and is already in **File > Build Settings** ahead of `Main.unity` so it will launch first.
- Drop a `SceneFlowController` on an empty GameObject in that scene to wire up buttons: call `LoadGameplayScene()` for your Play button, `LoadMainMenuScene()` for back/quit buttons, and `QuitGame()` for exiting builds.
- Add an `EventSystem` (already present) and your own Canvas/UI to design the menu; `MainMenuUI` can toggle a character-select panel and pass through start/quit actions.
- You can also place the `GameManager` in the main menu scene now—the singleton persists across scene loads, automatically grabs the `EnemySpawner` in whatever gameplay scene you open, and resets waves/kill counts each time you launch or replay a map. The `WeaponManager` and `UpgradeManager` can be kept there too; they will auto-link to scene objects on load and reset their internal state for each run.

## Character selection menu
- Create `FF/Character` assets (place them under `Assets/Resources/Characters` to keep them grouped) to describe each selectable character: set a display name, description, ability id, and optional portrait.
- Add those assets to a `CharacterSelectionUI` component in the main menu; hook its Next/Previous/Confirm methods to your UI buttons.
- The chosen `CharacterDefinition` is stored in `CharacterSelectionState.SelectedCharacter` and persists across scene loads so gameplay scripts can read the `AbilityId` later.

## Healing upgrade
- A new "Field Medic" upgrade heals the player for 35 HP without granting bonus stats. The Max Health upgrade now raises max HP without refilling current health to avoid accidental heals.
