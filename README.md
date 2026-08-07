# SmartRecon

SmartRecon is a unified Rust administration and investigation suite combining high-performance radar, secure vanish, player inspection, forensic tools, and vanish-only map teleportation. It runs as one self-contained plugin on Oxide or Carbon.

Version: **2.0.6**

## Highlights

- Tracks active players, humanoid NPCs, animals, sleeping players, dropped/world loot, hidden or exposed stashes, and tool cupboards.
- Provides a compact modern investigation panel with live independent layer toggles.
- Uses shared spatial indexes instead of scanning every tracked entity for every administrator.
- Runs all radar sessions through one staggered scheduler with configurable workload limits.
- Shows player names, health, distance, team color, voice activity, authorization level, and player state.
- Optionally shows held weapons and attachments, TC authorization counts, and player-to-TC links.
- Supports fixed-length vision arrows without infinite physics raycasts.
- Uses entity-parented drawings for connected players so labels follow moving targets.
- Supports player-name, team, authorization, and safe-zone filters.
- Remembers each administrator's preferred mode and settings.
- Supports temporary radar sessions that automatically expire.
- Provides true built-in network invisibility, noclip, metabolism protection, anti-hack bypass, damage protection, silent effects, and optional investigative interaction.
- Automatically starts radar with vision arrows when an administrator vanishes and stops radar when they reappear.
- Automatically starts radar with vision arrows during native Rust spectating and follows the watched player.
- Teleports permitted vanished administrators to right-click map markers, then automatically removes the temporary marker.
- Hides vanished players and server owners from unauthorized radar users by default.
- Does not modify player authorization flags.
- Includes bounded one-shot searches for Steam ID associations, twig/unprivileged building blocks, and nearby dropped loot.

## Why choose SmartRecon?

SmartRecon is intended for server owners who want capable administrative visibility without giving every staff member unrestricted access or letting each radar session repeatedly scan the entire server. Its shared spatial indexes, staggered scheduler, result limits, and drawing budget are designed to keep workload predictable as player and entity counts grow.

Compared with using separate radar and vanish plugins, SmartRecon provides one coordinated investigative lifecycle. Entering vanish can immediately enable the administrator's saved radar profile with viewing-direction arrows forced on; leaving vanish shuts that radar session down. Player, sleeper, stash, and tool-cupboard tracking, temporary sessions, filters, granular permissions, extended-range control, and vanished-staff privacy are managed by the same plugin.

That same workflow extends to rapid movement across the map. A vanished administrator with `smartrecon.vanish.teleport` can right-click a destination on the Rust map and teleport there immediately; SmartRecon removes the temporary marker after arrival so investigative notes do not accumulate. Ordinary markers remain untouched while the administrator is visible, and marker teleporting is disabled during native Rust spectating. Configurable landing behavior, a short anti-double-fire interval, and optional audit logging make the feature fast for staff without turning normal map-marker use into a teleport command.

SmartRecon replaces the need to run separate radar and vanish plugins for this workflow. Its vanish system removes administrators from normal networking and AI awareness, disables their collider, suppresses their signals and effects, protects them from damage, and can provide safe inventory or locked-entity interaction for authorized investigators. Expensive protection hooks are enabled only while at least one administrator is vanished.

## Requirements

- A current Rust dedicated server.
- Oxide/uMod or Carbon.
- No separate radar or Vanish plugin is required.

## Installation

1. Download [`SmartRecon.cs`](SmartRecon.cs).
2. Place it in your plugin directory:
   - Oxide: `oxide/plugins/`
   - Carbon: `carbon/plugins/`
3. Wait for the framework to compile and load the plugin.
4. Review the generated configuration file before granting access to non-administrators.

Remove or unload other plugins that register `/radar`, `/vanish`, or `/inv` before testing SmartRecon. Two plugins should not control the same player's limited-networking vanish state at the same time.

Rust moderators and owners bypass normal SmartRecon permissions by default through `Rust moderators and owners bypass SmartRecon permissions: true`. Regular players never receive this bypass. Set the option to `false` if every staff member should require individually assigned permissions.

## Quick start

```text
/vanish
/vanish status
/radar
/radar all
/radar players 250 1
/radar status
/radar for 60
/radar ui
/radar help
```

The default radar aliases are `/radar`, `/recon`, and `/smartrecon`. The default vanish aliases are `/vanish` and `/v`; inventory inspection uses `/inv` and `/invspy`. Aliases can be changed in the configuration.

## Investigative workflow

With the default configuration:

1. An authorized administrator runs `/vanish`.
2. SmartRecon makes the administrator invisible, enables noclip and protections, and starts the administrator's saved radar profile.
3. Player vision arrows are forced on for that temporary radar session without changing the saved arrows preference.
4. A compact right-side panel exposes Players, NPCs, Loot, Stashes, Tool Cupboards, Sleepers, Vision/Arrows, Extended Info, TC Links, and Voice toggles. Open the normal inventory screen to obtain a cursor and click it.
5. With `smartrecon.vanish.teleport`, placing a map marker instantly teleports the vanished administrator to that location and removes the temporary marker.
6. The administrator investigates using radar filters and, when permitted, inventory or reload-key interaction.
7. Running `/vanish` again makes the administrator visible, stops radar, and removes the panel automatically.

Vanish and radar can still be controlled independently when needed. Automatic linking, the fallback radar mode, and forced vision arrows are configurable.

## Vanish commands

| Command | What it does | Permission |
| --- | --- | --- |
| `/vanish` | Toggles the caller's built-in SmartRecon vanish state. | `smartrecon.vanish` |
| `/vanish on` | Enters vanish and starts the configured investigative radar profile. | `smartrecon.vanish` plus applicable radar permissions |
| `/vanish off` | Reappears and stops radar. Users with permanent vanish cannot turn it off. | `smartrecon.vanish` |
| `/vanish status` | Reports vanish, radar, and vision-arrow state. | `smartrecon.vanish` |
| `/vanish help` | Displays the built-in vanish command summary. | `smartrecon.vanish` |
| `/inv <name\|SteamID>` | Opens an active or sleeping player's main, wear, and belt inventory for inspection. With no argument, it checks the nearby player being looked at. | `smartrecon.vanish.inventory` |

`true` and `1` are accepted in place of `on`; `false` and `0` are accepted in place of `off`. Vanishing and reappearing play a private feedback sound for the administrator by default. The sound paths and optional nearby-player audibility are configurable.

## Radar modes

| Mode | Displays |
| --- | --- |
| `players` | Active players and, when enabled, sleeping players |
| `stashes` | Stashes, their distance, and hidden/exposed state |
| `tcs` | Tool cupboards and their distance |
| `all` | Players, stashes, and tool cupboards |
| `custom` | Any independent combination selected through `/radar layer` or the investigation panel |

## Radar commands

All radar commands require `smartrecon.use`. Commands that display or enable a particular type of information also require the corresponding feature permission shown below. With the default configuration, Rust moderators and owners bypass ordinary permissions; see [Permissions](#permissions) for the privacy exceptions.

| Command | What it does | Additional permission |
| --- | --- | --- |
| `/radar` | Toggles SmartRecon on or off using the user's saved mode and settings. | Permission for the saved mode |
| `/radar on` | Starts SmartRecon using the user's saved mode and settings. | Permission for the saved mode |
| `/radar off` | Stops the user's active radar session. | None |
| `/radar status` | Reports whether radar is active and displays its mode, distance, refresh rate, toggles, filters, and remaining temporary duration. | None |
| `/radar help` | Displays the built-in command summary. | None |
| `/radar ui` | Shows or hides the investigation panel. The panel can be clicked while the normal inventory cursor is open. | `smartrecon.ui` |
| `/radar reset` | Restores the user's saved preferences to configured defaults. If the default mode is not permitted, the first permitted mode is selected instead. | At least one mode permission |
| `/radar <players\|stashes\|tcs\|all> [distance] [rate]` | Selects a mode, optionally changes distance and refresh rate, and starts radar immediately. | Permission for every feature in the selected mode |
| `/radar mode <players\|stashes\|tcs\|all>` | Changes the saved mode and the active session's mode, if running. It does not start radar by itself. | Permission for every feature in the selected mode |
| `/radar distance <meters>` | Changes the saved search radius and the active session's radius, subject to the permitted maximum. | `smartrecon.extendedrange` only when exceeding the standard maximum |
| `/radar rate <seconds>` | Changes the saved refresh interval and the active session's interval, within configured limits. Static radar updates still respect the configured static minimum. | None |
| `/radar for <seconds>` | Starts radar if necessary and sets it to stop automatically after the specified duration. | Permission for the saved mode |
| `/radar arrows [on\|off]` | Toggles fixed-length viewing-direction arrows for player targets. Omitting the value toggles the current setting. | `smartrecon.arrows` |
| `/radar voice [on\|off]` | Toggles indicators for players who spoke recently. Omitting the value toggles the current setting. | `smartrecon.voice` |
| `/radar sleepers [on\|off]` | Toggles sleeping-player targets in player radar. Omitting the value toggles the current setting. | `smartrecon.sleepers` |
| `/radar vanished [on\|off]` | Toggles targets that SmartRecon identifies as vanished. Vanished targets remain hidden unless this is enabled and the viewer is permitted to see them. | `smartrecon.seevanished` |
| `/radar layer <players\|npcs\|loot\|stashes\|tcs> [on\|off]` | Independently toggles a radar category and switches the saved profile to `custom`. Omitting the value toggles the category. | Matching category permission |
| `/radar extended [on\|off]` | Toggles held-item and weapon-attachment details beneath player labels. | `smartrecon.extended` |
| `/radar tclinks [on\|off]` | Toggles player-to-nearest-authorized-TC arrows and TC authorization counts. | `smartrecon.tcinfo` |
| `/radar filter name <text\|off>` | Shows only players whose display name contains the supplied text; `off` clears the name filter. Multiple-word text is supported. | None |
| `/radar filter team <all\|mine\|others\|solo>` | Shows all players, the viewer's teammates, non-teammates, or players with no team. | None |
| `/radar filter auth <all\|players\|staff\|moderators\|owners>` | Filters targets by Rust authorization level: regular players, all staff, moderators, or owners. | None |
| `/radar filter safezone <all\|inside\|outside>` | Shows all players or only targets inside or outside safe zones. | None |
| `/radar findid <SteamID>` | Draws up to 250 owned, TC-authorized, bag-deployed, or code-lock-authorized entity matches for 30 seconds. The scan yields between bounded batches. | `smartrecon.forensics` |
| `/radar buildings <twig\|unprivileged>` | Draws up to 250 matching building blocks for 30 seconds using a time-sliced scan. | `smartrecon.forensics` |
| `/radar drops [distance]` | Performs a one-shot 30-second drawing of indexed nearby loot. | `smartrecon.forensics` |

The `players`, `stashes`, and `tcs` modes require `smartrecon.players`, `smartrecon.stashes`, and `smartrecon.cupboards`, respectively. The `all` mode requires all three permissions. Accepted mode synonyms include `player`, `stash`, `tc`, `cupboard`, and their plural forms.

For toggle commands, `true`, `1`, and `toggle` are accepted in addition to `on`; `false` and `0` are accepted in addition to `off`.

Filter aliases are also accepted: `team` for `mine`; `other` for `others`; `noteam` for `solo`; `player` for `players`; `admins` for `staff`; `moderator` or `mods` for `moderators`; `owner` for `owners`; and `in` or `out` for the corresponding safe-zone filters.

All numeric input is validated. Negative, zero, NaN, infinite, out-of-range, or otherwise invalid values are rejected without replacing a working radar session.

## Permissions

Radar feature permissions do not grant radar command access on their own; a user must also have `smartrecon.use`. Vanish permissions are separate and do not require `smartrecon.use`, although automatic investigative radar can start only when the user has the required radar permissions.

| Permission | What it grants |
| --- | --- |
| `smartrecon.use` | Access to `/radar`, `/recon`, and `/smartrecon`. It does not grant access to any target category by itself. |
| `smartrecon.players` | Permission to select player radar and include players in `all` mode. |
| `smartrecon.stashes` | Permission to select stash radar and include hidden or exposed stashes in `all` mode. |
| `smartrecon.cupboards` | Permission to select tool-cupboard radar and include cupboards in `all` mode. The command name for this mode is `tcs`. |
| `smartrecon.npcs` | Permission to enable the independent NPC layer, including humanoid NPCs and supported animals such as bears, wolves, boars, stags, chickens, sharks, horses, wildlife hazards, and vendors. |
| `smartrecon.loot` | Permission to enable the bounded dropped/world-loot radar layer. |
| `smartrecon.arrows` | Permission to enable fixed-length viewing-direction arrows on player targets. |
| `smartrecon.voice` | Permission to enable recent voice-activity indicators on player targets. |
| `smartrecon.sleepers` | Permission to include sleeping players in player or `all` radar. |
| `smartrecon.extendedrange` | Permission to select distances above the configured standard maximum, up to the configured extended maximum. It does not grant a radar mode. |
| `smartrecon.extended` | Permission to display held-item and attachment details in player labels. |
| `smartrecon.tcinfo` | Permission to display TC authorization counts and player-to-authorized-TC links. |
| `smartrecon.ui` | Permission to display and operate the onscreen investigation panel. |
| `smartrecon.forensics` | Permission to run the bounded `findid`, `buildings`, and `drops` one-shot searches. |
| `smartrecon.seevanished` | Permission to enable and display vanished-player targets. This is a privacy-sensitive permission with stricter administrator-bypass rules. |
| `smartrecon.seeowners` | Permission for a moderator to display owner-level targets when owner hiding is enabled. This is a privacy-sensitive permission with stricter administrator-bypass rules. |
| `smartrecon.vanish` | Permission to use `/vanish` and become invisible. This does not grant radar or investigative interaction features by itself. |
| `smartrecon.vanish.permanent` | Forces a permitted user to remain vanished and restores vanish on connection. This lockout-sensitive permission must be granted directly to the user; administrator bypass and group/wildcard grants do not activate it. |
| `smartrecon.vanish.unlock` | Allows a vanished investigator to bypass locks, toggle targeted doors, and mount targeted vehicles using reload-key interaction. |
| `smartrecon.vanish.damage` | Allows a vanished investigator to deal damage when outgoing vanished damage is otherwise blocked. |
| `smartrecon.vanish.inventory` | Allows `/inv`, `/invspy`, and reload-key inspection of player or storage-container inventories. |
| `smartrecon.vanish.teleport` | Allows a vanished administrator to place a map marker and immediately teleport to it. Visible players are ignored, and the temporary teleport marker is removed by default. |

Example Oxide grants:

```text
oxide.grant user 76561198000000000 smartrecon.use
oxide.grant user 76561198000000000 smartrecon.players
oxide.grant user 76561198000000000 smartrecon.stashes
oxide.grant user 76561198000000000 smartrecon.cupboards
oxide.grant user 76561198000000000 smartrecon.npcs
oxide.grant user 76561198000000000 smartrecon.loot
oxide.grant user 76561198000000000 smartrecon.arrows
oxide.grant user 76561198000000000 smartrecon.ui
oxide.grant user 76561198000000000 smartrecon.vanish
oxide.grant user 76561198000000000 smartrecon.vanish.inventory
```

For Carbon, use the equivalent Carbon permission commands or permission interface.

### Privacy permissions

`smartrecon.seevanished` and `smartrecon.seeowners` are handled more strictly than ordinary feature permissions. With administrator permission bypass enabled:

- Owner-level administrators may use these privacy capabilities.
- Moderator-level administrators still require the explicit privacy permission.
- A vanished player is hidden unless the viewer both enables vanished targets and is permitted to see them.
- An owner is hidden from a moderator unless the moderator has `smartrecon.seeowners`.

## Built-in vanish

SmartRecon's vanish is self-contained. It uses Rust's limited-networking state, removes the administrator from ordinary network subscribers and server entity queries, tells AI memory to ignore the administrator, disables the collider, and keeps network groups updated while the administrator moves. It can enable noclip, pause metabolism, bypass anti-hack violations, block incoming and outgoing damage, and suppress entity signals and effects that could reveal the investigator.

While vanished, pressing the reload key while looking at a permitted target can inspect a player or container, toggle a door, or mount a vehicle. Inventory inspection and lock bypass are independently permission-controlled. With `smartrecon.vanish.teleport`, placing a map marker teleports the vanished administrator immediately; no reload-key modifier is required. Visible-player markers retain normal Rust behavior. Entering native Rust spectating cleanly leaves SmartRecon vanish so Rust's spectator networking can take control, then starts radar with vision arrows and centers all distance queries on the watched player. Marker teleporting remains explicitly disabled throughout spectating.

Successful marker teleports are written to SmartRecon's separate `teleports` audit log by default. Each entry records UTC time, administrator name and Steam ID, starting coordinates, and destination coordinates. This can be disabled in configuration without affecting teleport behavior.

Vanish state can persist across disconnects and plugin reloads. The `smartrecon.vanish.permanent` permission forces vanish to be restored and prevents manual reappearance. SmartRecon exposes `Disappear`, `Reappear`, `IsInvisible`, `_Disappear`, `_Reappear`, and `_IsInvisible` for vanish compatibility. Radar integrations can call `IsRadarEnabled`, `EnableRadar`, `DisableRadar`, and `IsRadarLayerEnabled`. Lifecycle hooks include `OnSmartReconActivated`, `OnSmartReconDeactivated`, `OnSmartInvestigationStarted`, and `OnSmartInvestigationEnded`, in addition to the familiar `OnVanishDisappear` and `OnVanishReappear` veto hooks.

## Performance design

SmartRecon is designed to avoid the most expensive behavior found in simple ESP implementations:

- Active players, humanoid NPCs, moving animals, sleepers, stashes, cupboards, and tracked loot are assigned to configurable map cells.
- A radar request searches only cells intersecting its radius.
- Moving-player and animal/NPC indexes are rebuilt once and shared by all radar users.
- Player indexing sleeps completely when nobody is using player radar.
- Stashes, cupboards, and loot are updated through entity spawn and kill hooks.
- Static entities refresh less frequently than moving players by default.
- Results are sorted by squared distance, and only the nearest configured number are drawn.
- A per-session draw-command budget prevents a large result set from creating an unlimited burst.
- Session deadlines are staggered, and only a configured number of sessions can update per scheduler tick.
- The CUI panel is rebuilt only when its state changes; it does not refresh every scheduler cycle.
- Forensic searches are permission-gated, limited to 250 drawings, protected by a cooldown, and yield every 200 inspected entities.
- Voice hooks are subscribed only while at least one active session requests voice indicators.
- Vanish damage, lock, anti-hack, collider, and marker hooks are subscribed only while someone is vanished and only when their feature is configured.
- Harmony patches perform small constant-time exits when nobody is vanished and suppress investigator signals or effects only when needed.

The defaults are deliberately conservative. Increasing range, result limits, or refresh frequency increases server and client workload.

## Configuration

SmartRecon creates a readable JSON configuration after its first load. A complete copy of the default configuration is available at [`config/SmartRecon.example.json`](config/SmartRecon.example.json).

Important defaults:

- Standard maximum distance: `250m`
- Extended maximum distance: `1000m`
- Player refresh rate: `1s`
- Static-entity minimum refresh: `2s`
- Vision arrows: disabled
- Vision arrows while vanish starts radar: forced on
- Radar during native Rust spectating: starts automatically with vision arrows forced on
- Spectate-started radar: stops automatically when native spectating ends
- Private vanish and reappear feedback sounds: enabled
- Vanish-only map-marker teleport: enabled for users with `smartrecon.vanish.teleport`
- Used teleport markers: removed automatically
- Successful marker teleports: written to a separate audit log
- Investigation panel: enabled and shown when radar starts
- NPC, loot, extended-info, and TC-link layers: disabled until requested
- Voice indicators: disabled
- Sleeping players: disabled
- Vanished players: hidden
- Owners hidden from moderators: enabled
- Rust administrator permission bypass: enabled
- Radar starts when entering vanish: enabled
- Radar stops when leaving vanish: enabled
- Vanish persistence: enabled
- Noclip, metabolism protection, anti-hack bypass, and damage protection while vanished: enabled

Configuration values are normalized on load to prevent unsafe scheduler intervals, ranges, cell sizes, and result limits.

## Stored data

When preference persistence is enabled, SmartRecon stores each administrator's last mode, distance, rate, toggles, and filters in its framework data directory. When vanish persistence is enabled, it also stores which administrators should remain vanished across disconnects or reloads. Active radar sessions and temporary expiry times are not restored independently; the investigative radar session is recreated when persisted vanish is restored.

## Compatibility

SmartRecon is implemented as an Oxide-compatible `RustPlugin` and does not depend on Carbon-only APIs. Its self-contained Harmony patches use the patching support supplied by the server framework to isolate vanished-player sounds and effects. The same source is intended for Oxide and Carbon.

Version 1.3.1 was compile-checked against local Rust/Oxide assemblies. Its vanish movement updater supports both known Rust `UpdateGroups` signatures, and its NPC tracking recognizes both older and newer Rust NPC base types without depending on the connected-player list. Final runtime validation should be performed on a current test server before production deployment.

## Changelog

See [`CHANGELOG.md`](CHANGELOG.md).

## License

SmartRecon is available under the [MIT License](LICENSE).
