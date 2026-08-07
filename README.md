# SmartRadar

SmartRadar is a unified administrative vanish and high-performance radar plugin for Rust servers. It is designed for investigative work and runs as one self-contained plugin on Oxide or Carbon.

Version: **1.2.0**

## Highlights

- Tracks active players, NPCs, sleeping players, dropped/world loot, hidden or exposed stashes, and tool cupboards.
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
- Hides vanished players and server owners from unauthorized radar users by default.
- Does not modify player authorization flags.
- Includes bounded one-shot searches for Steam ID associations, twig/unprivileged building blocks, and nearby dropped loot.

## Why choose SmartRadar?

SmartRadar is intended for server owners who want capable administrative visibility without giving every staff member unrestricted access or letting each radar session repeatedly scan the entire server. Its shared spatial indexes, staggered scheduler, result limits, and drawing budget are designed to keep workload predictable as player and entity counts grow.

Compared with using separate radar and vanish plugins, SmartRadar provides one coordinated investigative lifecycle. Entering vanish can immediately enable the administrator's saved radar profile with viewing-direction arrows forced on; leaving vanish shuts that radar session down. Player, sleeper, stash, and tool-cupboard tracking, temporary sessions, filters, granular permissions, extended-range control, and vanished-staff privacy are managed by the same plugin.

SmartRadar replaces the need to run separate radar and vanish plugins for this workflow. Its vanish system removes administrators from normal networking and AI awareness, disables their collider, suppresses their signals and effects, protects them from damage, and can provide safe inventory or locked-entity interaction for authorized investigators. Expensive protection hooks are enabled only while at least one administrator is vanished.

## Requirements

- A current Rust dedicated server.
- Oxide/uMod or Carbon.
- No separate radar or Vanish plugin is required.

## Installation

1. Download [`SmartRadar.cs`](SmartRadar.cs).
2. Place it in your plugin directory:
   - Oxide: `oxide/plugins/`
   - Carbon: `carbon/plugins/`
3. Wait for the framework to compile and load the plugin.
4. Review the generated configuration file before granting access to non-administrators.

Remove or unload other plugins that register `/radar`, `/vanish`, or `/inv` before testing SmartRadar. Two plugins should not control the same player's limited-networking vanish state at the same time.

Rust moderators and owners bypass normal SmartRadar permissions by default. This can be disabled in the configuration.

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

The default radar aliases are `/radar`, `/sradar`, and `/smartradar`. The default vanish aliases are `/vanish` and `/v`; inventory inspection uses `/inv` and `/invspy`. Aliases can be changed in the configuration.

## Investigative workflow

With the default configuration:

1. An authorized administrator runs `/vanish`.
2. SmartRadar makes the administrator invisible, enables noclip and protections, and starts the administrator's saved radar profile.
3. Player vision arrows are forced on for that temporary radar session without changing the saved arrows preference.
4. A compact right-side panel exposes Players, NPCs, Loot, Stashes, Tool Cupboards, Sleepers, Vision/Arrows, Extended Info, TC Links, and Voice toggles. Open the normal inventory screen to obtain a cursor and click it.
5. The administrator investigates using radar filters and, when permitted, inventory or reload-key interaction.
6. Running `/vanish` again makes the administrator visible, stops radar, and removes the panel automatically.

Vanish and radar can still be controlled independently when needed. Automatic linking, the fallback radar mode, and forced vision arrows are configurable.

## Vanish commands

| Command | What it does | Permission |
| --- | --- | --- |
| `/vanish` | Toggles the caller's built-in SmartRadar vanish state. | `smartradar.vanish` |
| `/vanish on` | Enters vanish and starts the configured investigative radar profile. | `smartradar.vanish` plus applicable radar permissions |
| `/vanish off` | Reappears and stops radar. Users with permanent vanish cannot turn it off. | `smartradar.vanish` |
| `/vanish status` | Reports vanish, radar, and vision-arrow state. | `smartradar.vanish` |
| `/vanish help` | Displays the built-in vanish command summary. | `smartradar.vanish` |
| `/inv <name\|SteamID>` | Opens an active or sleeping player's main, wear, and belt inventory for inspection. With no argument, it checks the nearby player being looked at. | `smartradar.vanish.inventory` |

`true` and `1` are accepted in place of `on`; `false` and `0` are accepted in place of `off`.

## Radar modes

| Mode | Displays |
| --- | --- |
| `players` | Active players and, when enabled, sleeping players |
| `stashes` | Stashes, their distance, and hidden/exposed state |
| `tcs` | Tool cupboards and their distance |
| `all` | Players, stashes, and tool cupboards |
| `custom` | Any independent combination selected through `/radar layer` or the investigation panel |

## Radar commands

All radar commands require `smartradar.use`. Commands that display or enable a particular type of information also require the corresponding feature permission shown below. With the default configuration, Rust moderators and owners bypass ordinary permissions; see [Permissions](#permissions) for the privacy exceptions.

| Command | What it does | Additional permission |
| --- | --- | --- |
| `/radar` | Toggles SmartRadar on or off using the user's saved mode and settings. | Permission for the saved mode |
| `/radar on` | Starts SmartRadar using the user's saved mode and settings. | Permission for the saved mode |
| `/radar off` | Stops the user's active radar session. | None |
| `/radar status` | Reports whether radar is active and displays its mode, distance, refresh rate, toggles, filters, and remaining temporary duration. | None |
| `/radar help` | Displays the built-in command summary. | None |
| `/radar ui` | Shows or hides the investigation panel. The panel can be clicked while the normal inventory cursor is open. | `smartradar.ui` |
| `/radar reset` | Restores the user's saved preferences to configured defaults. If the default mode is not permitted, the first permitted mode is selected instead. | At least one mode permission |
| `/radar <players\|stashes\|tcs\|all> [distance] [rate]` | Selects a mode, optionally changes distance and refresh rate, and starts radar immediately. | Permission for every feature in the selected mode |
| `/radar mode <players\|stashes\|tcs\|all>` | Changes the saved mode and the active session's mode, if running. It does not start radar by itself. | Permission for every feature in the selected mode |
| `/radar distance <meters>` | Changes the saved search radius and the active session's radius, subject to the permitted maximum. | `smartradar.extendedrange` only when exceeding the standard maximum |
| `/radar rate <seconds>` | Changes the saved refresh interval and the active session's interval, within configured limits. Static radar updates still respect the configured static minimum. | None |
| `/radar for <seconds>` | Starts radar if necessary and sets it to stop automatically after the specified duration. | Permission for the saved mode |
| `/radar arrows [on\|off]` | Toggles fixed-length viewing-direction arrows for player targets. Omitting the value toggles the current setting. | `smartradar.arrows` |
| `/radar voice [on\|off]` | Toggles indicators for players who spoke recently. Omitting the value toggles the current setting. | `smartradar.voice` |
| `/radar sleepers [on\|off]` | Toggles sleeping-player targets in player radar. Omitting the value toggles the current setting. | `smartradar.sleepers` |
| `/radar vanished [on\|off]` | Toggles targets that SmartRadar identifies as vanished. Vanished targets remain hidden unless this is enabled and the viewer is permitted to see them. | `smartradar.seevanished` |
| `/radar layer <players\|npcs\|loot\|stashes\|tcs> [on\|off]` | Independently toggles a radar category and switches the saved profile to `custom`. Omitting the value toggles the category. | Matching category permission |
| `/radar extended [on\|off]` | Toggles held-item and weapon-attachment details beneath player labels. | `smartradar.extended` |
| `/radar tclinks [on\|off]` | Toggles player-to-nearest-authorized-TC arrows and TC authorization counts. | `smartradar.tcinfo` |
| `/radar filter name <text\|off>` | Shows only players whose display name contains the supplied text; `off` clears the name filter. Multiple-word text is supported. | None |
| `/radar filter team <all\|mine\|others\|solo>` | Shows all players, the viewer's teammates, non-teammates, or players with no team. | None |
| `/radar filter auth <all\|players\|staff\|moderators\|owners>` | Filters targets by Rust authorization level: regular players, all staff, moderators, or owners. | None |
| `/radar filter safezone <all\|inside\|outside>` | Shows all players or only targets inside or outside safe zones. | None |
| `/radar findid <SteamID>` | Draws up to 250 owned, TC-authorized, bag-deployed, or code-lock-authorized entity matches for 30 seconds. The scan yields between bounded batches. | `smartradar.forensics` |
| `/radar buildings <twig\|unprivileged>` | Draws up to 250 matching building blocks for 30 seconds using a time-sliced scan. | `smartradar.forensics` |
| `/radar drops [distance]` | Performs a one-shot 30-second drawing of indexed nearby loot. | `smartradar.forensics` |

The `players`, `stashes`, and `tcs` modes require `smartradar.players`, `smartradar.stashes`, and `smartradar.cupboards`, respectively. The `all` mode requires all three permissions. Accepted mode synonyms include `player`, `stash`, `tc`, `cupboard`, and their plural forms.

For toggle commands, `true`, `1`, and `toggle` are accepted in addition to `on`; `false` and `0` are accepted in addition to `off`.

Filter aliases are also accepted: `team` for `mine`; `other` for `others`; `noteam` for `solo`; `player` for `players`; `admins` for `staff`; `moderator` or `mods` for `moderators`; `owner` for `owners`; and `in` or `out` for the corresponding safe-zone filters.

Legacy Simple Radar syntax is also supported:

```text
/radar <rate> <distance> <mode>
```

All numeric input is validated. Negative, zero, NaN, infinite, out-of-range, or otherwise invalid values are rejected without replacing a working radar session.

## Permissions

Radar feature permissions do not grant radar command access on their own; a user must also have `smartradar.use`. Vanish permissions are separate and do not require `smartradar.use`, although automatic investigative radar can start only when the user has the required radar permissions.

| Permission | What it grants |
| --- | --- |
| `smartradar.use` | Access to the `/radar`, `/sradar`, and `/smartradar` command aliases. It does not grant access to any target category by itself. |
| `smartradar.players` | Permission to select player radar and include players in `all` mode. |
| `smartradar.stashes` | Permission to select stash radar and include hidden or exposed stashes in `all` mode. |
| `smartradar.cupboards` | Permission to select tool-cupboard radar and include cupboards in `all` mode. The command name for this mode is `tcs`. |
| `smartradar.npcs` | Permission to enable the independent NPC radar layer. |
| `smartradar.loot` | Permission to enable the bounded dropped/world-loot radar layer. |
| `smartradar.arrows` | Permission to enable fixed-length viewing-direction arrows on player targets. |
| `smartradar.voice` | Permission to enable recent voice-activity indicators on player targets. |
| `smartradar.sleepers` | Permission to include sleeping players in player or `all` radar. |
| `smartradar.extendedrange` | Permission to select distances above the configured standard maximum, up to the configured extended maximum. It does not grant a radar mode. |
| `smartradar.extended` | Permission to display held-item and attachment details in player labels. |
| `smartradar.tcinfo` | Permission to display TC authorization counts and player-to-authorized-TC links. |
| `smartradar.ui` | Permission to display and operate the onscreen investigation panel. |
| `smartradar.forensics` | Permission to run the bounded `findid`, `buildings`, and `drops` one-shot searches. |
| `smartradar.seevanished` | Permission to enable and display vanished-player targets. This is a privacy-sensitive permission with stricter administrator-bypass rules. |
| `smartradar.seeowners` | Permission for a moderator to display owner-level targets when owner hiding is enabled. This is a privacy-sensitive permission with stricter administrator-bypass rules. |
| `smartradar.vanish` | Permission to use `/vanish` and become invisible. This does not grant radar or investigative interaction features by itself. |
| `smartradar.vanish.permanent` | Forces a permitted user to remain vanished and restores vanish on connection. This permission is always explicit and is never inherited through administrator bypass. |
| `smartradar.vanish.unlock` | Allows a vanished investigator to bypass locks, toggle targeted doors, and mount targeted vehicles using reload-key interaction. |
| `smartradar.vanish.damage` | Allows a vanished investigator to deal damage when outgoing vanished damage is otherwise blocked. |
| `smartradar.vanish.inventory` | Allows `/inv`, `/invspy`, and reload-key inspection of player or storage-container inventories. |
| `smartradar.vanish.teleport` | Allows reload plus map-marker teleport while vanished when that optional configuration feature is enabled. |

Example Oxide grants:

```text
oxide.grant user 76561198000000000 smartradar.use
oxide.grant user 76561198000000000 smartradar.players
oxide.grant user 76561198000000000 smartradar.stashes
oxide.grant user 76561198000000000 smartradar.cupboards
oxide.grant user 76561198000000000 smartradar.npcs
oxide.grant user 76561198000000000 smartradar.loot
oxide.grant user 76561198000000000 smartradar.arrows
oxide.grant user 76561198000000000 smartradar.ui
oxide.grant user 76561198000000000 smartradar.vanish
oxide.grant user 76561198000000000 smartradar.vanish.inventory
```

For Carbon, use the equivalent Carbon permission commands or permission interface.

### Privacy permissions

`smartradar.seevanished` and `smartradar.seeowners` are handled more strictly than ordinary feature permissions. With administrator permission bypass enabled:

- Owner-level administrators may use these privacy capabilities.
- Moderator-level administrators still require the explicit privacy permission.
- A vanished player is hidden unless the viewer both enables vanished targets and is permitted to see them.
- An owner is hidden from a moderator unless the moderator has `smartradar.seeowners`.

## Built-in vanish

SmartRadar's vanish is self-contained. It uses Rust's limited-networking state, removes the administrator from ordinary network subscribers and server entity queries, tells AI memory to ignore the administrator, disables the collider, and keeps network groups updated while the administrator moves. It can enable noclip, pause metabolism, bypass anti-hack violations, block incoming and outgoing damage, and suppress entity signals and effects that could reveal the investigator.

While vanished, pressing the reload key while looking at a permitted target can inspect a player or container, toggle a door, or mount a vehicle. Inventory inspection and lock bypass are independently permission-controlled. Reload plus map-marker teleport is available but disabled by default.

Vanish state can persist across disconnects and plugin reloads. The `smartradar.vanish.permanent` permission forces vanish to be restored and prevents manual reappearance. SmartRadar exposes `Disappear`, `Reappear`, `IsInvisible`, `_Disappear`, `_Reappear`, and `_IsInvisible` for vanish compatibility. Radar integrations can call `IsRadarEnabled`, `EnableRadar`, `DisableRadar`, and `IsRadarLayerEnabled`. Lifecycle hooks include `OnSmartRadarActivated`, `OnSmartRadarDeactivated`, `OnSmartInvestigationStarted`, and `OnSmartInvestigationEnded`, in addition to the familiar `OnVanishDisappear` and `OnVanishReappear` veto hooks.

## Performance design

SmartRadar is designed to avoid the most expensive behavior found in simple ESP implementations:

- Active players, sleepers, stashes, cupboards, and tracked loot are assigned to configurable map cells.
- A radar request searches only cells intersecting its radius.
- Moving-player indexes are rebuilt once and shared by all radar users.
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

SmartRadar creates a readable JSON configuration after its first load. A complete copy of the default configuration is available at [`config/SmartRadar.example.json`](config/SmartRadar.example.json).

Important defaults:

- Standard maximum distance: `250m`
- Extended maximum distance: `1000m`
- Player refresh rate: `1s`
- Static-entity minimum refresh: `2s`
- Vision arrows: disabled
- Vision arrows while vanish starts radar: forced on
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

When preference persistence is enabled, SmartRadar stores each administrator's last mode, distance, rate, toggles, and filters in its framework data directory. When vanish persistence is enabled, it also stores which administrators should remain vanished across disconnects or reloads. Active radar sessions and temporary expiry times are not restored independently; the investigative radar session is recreated when persisted vanish is restored.

## Compatibility

SmartRadar is implemented as an Oxide-compatible `RustPlugin` and does not depend on Carbon-only APIs. Its self-contained Harmony patches use the patching support supplied by the server framework to isolate vanished-player sounds and effects. The same source is intended for Oxide and Carbon.

Version 1.2.0 was compile-checked against local Rust/Oxide assemblies. Its vanish movement updater supports both known Rust `UpdateGroups` signatures. Final runtime validation should be performed on a current test server before production deployment.

## Changelog

See [`CHANGELOG.md`](CHANGELOG.md).

## License

SmartRadar is available under the [MIT License](LICENSE).
