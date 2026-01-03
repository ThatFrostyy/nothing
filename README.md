# Discord Rich Presence

This project uses the Discord Social SDK package to broadcast what the player is doing directly to Discord Rich Presence.

## Early-game player hook ideas
- Start the first wave with a short, celebratory banner or audio sting so new players feel the intensity spike immediately.
- Give a tiny, guaranteed early upgrade or perk within the first minute to reinforce progression and experimentation.
- Add a quick "first goal" callout (ex: “Survive wave 1 to unlock your first perk”) to provide immediate direction.
- Surface a brief, skippable tip about movement/aiming or a signature ability right after the player spawns.
- Include a small positive feedback loop (coins, XP burst, or a visual flourish) for the first few kills to build momentum.

## How it is set up
1. **Discord application**
   - Create a Discord application for the game and copy the **Application ID**.
   - Upload the Rich Presence art assets you want to display and note their keys (for example `logo` for the large image and any small badge you want to show).

2. **Scene wiring**
   - Add the `DiscordRichPresence` component (found under `Assets/Scripts/Core`) to the same persistent GameObject you use for `SceneFlowController` so it lives across menu and gameplay scenes.
   - Paste the Application ID into the component and keep **Persist Across Scenes** enabled.

3. **Presence text and art**
   - Set the **Large/Small Image Keys** and tooltips to match the assets configured in the Discord developer portal.
   - Edit the **Menu Details/State** fields for the main menu presence.
   - The gameplay presence uses `gameplayDetailsFormat` and `gameplayStateFormat` to show the current wave and total kills by subscribing to `GameManager` when the `Main` scene is active.

4. **Runtime behavior**
   - When the app is open, the Social SDK client is initialized with the supplied Application ID and updates Discord using `ActivityManager.UpdateActivity`.
   - The component listens for scene changes to swap between menu and gameplay presence, and it polls the SDK each frame in `Update` for callbacks.
   - The **Start Timestamp** is set automatically so Discord shows session duration.

5. **Optional custom updates**
   - Any script can override the generated text by calling `DiscordRichPresence.Instance.SetCustomPresence("Details text", "State text");` (for example, to show pause or boss fight status).

6. **Testing**
   - Launch Discord, run the game, and confirm the activity card shows the menu text when you are in `MainMenu` and live wave/kill counts when you are in `Main`.
