# SmartRadar

SmartRadar is a high-performance, privacy-aware administrative radar for Rust servers. It is compatible with Oxide and Carbon and can integrate with Vanish without making Vanish a required dependency.

Version: **1.0.0**

## Highlights

- Tracks active players, sleeping players, hidden or exposed stashes, and tool cupboards.
- Uses shared spatial indexes instead of scanning every tracked entity for every administrator.
- Runs all radar sessions through one staggered scheduler with configurable workload limits.
- Shows player names, health, distance, team color, voice activity, authorization level, and player state.
- Supports fixed-length vision arrows without infinite physics raycasts.
- Uses entity-parented drawings for connected players so labels follow moving targets.
- Supports player-name, team, authorization, and safe-zone filters.
- Remembers each administrator's preferred mode and settings.
- Supports temporary radar sessions that automatically expire.
- Hides vanished players and server owners from moderators by default.
- Does not modify player authorization flags or require Harmony patches.

## Requirements

- A current Rust dedicated server.
- Oxide/uMod or Carbon.
- Vanish is optional. When present, SmartRadar uses its API to identify invisible players.

## Installation

1. Download [`SmartRadar.cs`](SmartRadar.cs).
2. Place it in your plugin directory:
   - Oxide: `oxide/plugins/`
   - Carbon: `carbon/plugins/`
3. Wait for the framework to compile and load the plugin.
4. Review the generated configuration file before granting access to non-administrators.

Rust moderators and owners bypass normal SmartRadar permissions by default. This can be disabled in the configuration.

## Quick start

```text
/radar
/radar all
/radar players 250 1
/radar status
/radar for 60
/radar help
```

The default aliases are `/radar`, `/sradar`, and `/smartradar`. Every command shown below can use any of these aliases. Command aliases can also be changed in the configuration.

## Radar modes

| Mode | Displays |
| --- | --- |
| `players` | Active players and, when enabled, sleeping players |
| `stashes` | Stashes, their distance, and hidden/exposed state |
| `tcs` | Tool cupboards and their distance |
| `all` | Players, stashes, and tool cupboards |

## Commands

All commands require `smartradar.use`. Commands that display or enable a particular type of information also require the corresponding feature permission shown below. With the default configuration, Rust moderators and owners bypass ordinary permissions; see [Permissions](#permissions) for the privacy exceptions.

| Command | What it does | Additional permission |
| --- | --- | --- |
| `/radar` | Toggles SmartRadar on or off using the user's saved mode and settings. | Permission for the saved mode |
| `/radar on` | Starts SmartRadar using the user's saved mode and settings. | Permission for the saved mode |
| `/radar off` | Stops the user's active radar session. | None |
| `/radar status` | Reports whether radar is active and displays its mode, distance, refresh rate, toggles, filters, and remaining temporary duration. | None |
| `/radar help` | Displays the built-in command summary. | None |
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
| `/radar filter name <text\|off>` | Shows only players whose display name contains the supplied text; `off` clears the name filter. Multiple-word text is supported. | None |
| `/radar filter team <all\|mine\|others\|solo>` | Shows all players, the viewer's teammates, non-teammates, or players with no team. | None |
| `/radar filter auth <all\|players\|staff\|moderators\|owners>` | Filters targets by Rust authorization level: regular players, all staff, moderators, or owners. | None |
| `/radar filter safezone <all\|inside\|outside>` | Shows all players or only targets inside or outside safe zones. | None |

The `players`, `stashes`, and `tcs` modes require `smartradar.players`, `smartradar.stashes`, and `smartradar.cupboards`, respectively. The `all` mode requires all three permissions. Accepted mode synonyms include `player`, `stash`, `tc`, `cupboard`, and their plural forms.

For toggle commands, `true`, `1`, and `toggle` are accepted in addition to `on`; `false` and `0` are accepted in addition to `off`.

Filter aliases are also accepted: `team` for `mine`; `other` for `others`; `noteam` for `solo`; `player` for `players`; `admins` for `staff`; `moderator` or `mods` for `moderators`; `owner` for `owners`; and `in` or `out` for the corresponding safe-zone filters.

Legacy Simple Radar syntax is also supported:

```text
/radar <rate> <distance> <mode>
```

All numeric input is validated. Negative, zero, NaN, infinite, out-of-range, or otherwise invalid values are rejected without replacing a working radar session.

## Permissions

Possessing a feature permission does not grant command access on its own; a user must also have `smartradar.use`. Mode permissions control which target categories can be selected, while optional feature permissions control the extra information that may be enabled.

| Permission | What it grants |
| --- | --- |
| `smartradar.use` | Access to the `/radar`, `/sradar`, and `/smartradar` command aliases. It does not grant access to any target category by itself. |
| `smartradar.players` | Permission to select player radar and include players in `all` mode. |
| `smartradar.stashes` | Permission to select stash radar and include hidden or exposed stashes in `all` mode. |
| `smartradar.cupboards` | Permission to select tool-cupboard radar and include cupboards in `all` mode. The command name for this mode is `tcs`. |
| `smartradar.arrows` | Permission to enable fixed-length viewing-direction arrows on player targets. |
| `smartradar.voice` | Permission to enable recent voice-activity indicators on player targets. |
| `smartradar.sleepers` | Permission to include sleeping players in player or `all` radar. |
| `smartradar.extendedrange` | Permission to select distances above the configured standard maximum, up to the configured extended maximum. It does not grant a radar mode. |
| `smartradar.seevanished` | Permission to enable and display vanished-player targets. This is a privacy-sensitive permission with stricter administrator-bypass rules. |
| `smartradar.seeowners` | Permission for a moderator to display owner-level targets when owner hiding is enabled. This is a privacy-sensitive permission with stricter administrator-bypass rules. |

Example Oxide grants:

```text
oxide.grant user 76561198000000000 smartradar.use
oxide.grant user 76561198000000000 smartradar.players
oxide.grant user 76561198000000000 smartradar.stashes
oxide.grant user 76561198000000000 smartradar.cupboards
```

For Carbon, use the equivalent Carbon permission commands or permission interface.

### Privacy permissions

`smartradar.seevanished` and `smartradar.seeowners` are handled more strictly than ordinary feature permissions. With administrator permission bypass enabled:

- Owner-level administrators may use these privacy capabilities.
- Moderator-level administrators still require the explicit privacy permission.
- A vanished player is hidden unless the viewer both enables vanished targets and is permitted to see them.
- An owner is hidden from a moderator unless the moderator has `smartradar.seeowners`.

## Vanish integration

Vanish is optional and does not need to be declared as a hard dependency. SmartRadar checks the current Vanish API and briefly caches the result to avoid repeated cross-plugin calls during the same player-index interval.

If Vanish is unavailable or its API cannot be queried, SmartRadar can optionally treat Rust's limited-networking state as vanished. That fallback is enabled by default.

SmartRadar never changes Vanish state.

## Performance design

SmartRadar is designed to avoid the most expensive behavior found in simple ESP implementations:

- Active players, sleepers, stashes, and cupboards are assigned to configurable map cells.
- A radar request searches only cells intersecting its radius.
- Moving-player indexes are rebuilt once and shared by all radar users.
- Player indexing sleeps completely when nobody is using player radar.
- Stashes and cupboards are updated through entity spawn and kill hooks.
- Static entities refresh less frequently than moving players by default.
- Results are sorted by squared distance, and only the nearest configured number are drawn.
- A per-session draw-command budget prevents a large result set from creating an unlimited burst.
- Session deadlines are staggered, and only a configured number of sessions can update per scheduler tick.
- Voice hooks are subscribed only while at least one active session requests voice indicators.

The defaults are deliberately conservative. Increasing range, result limits, or refresh frequency increases server and client workload.

## Configuration

SmartRadar creates a readable JSON configuration after its first load. A complete copy of the default configuration is available at [`config/SmartRadar.example.json`](config/SmartRadar.example.json).

Important defaults:

- Standard maximum distance: `250m`
- Extended maximum distance: `1000m`
- Player refresh rate: `1s`
- Static-entity minimum refresh: `2s`
- Vision arrows: disabled
- Voice indicators: disabled
- Sleeping players: disabled
- Vanished players: hidden
- Owners hidden from moderators: enabled
- Rust administrator permission bypass: enabled

Configuration values are normalized on load to prevent unsafe scheduler intervals, ranges, cell sizes, and result limits.

## Stored data

When preference persistence is enabled, SmartRadar stores each administrator's last mode, distance, rate, toggles, and filters in its framework data directory. Active radar sessions and temporary expiry times are not restored after a reload or restart.

## Compatibility

SmartRadar is implemented as an Oxide-compatible `RustPlugin`. It does not depend on Carbon-only APIs and does not require custom Harmony patches, allowing the same source file to run under Oxide or Carbon.

Version 1.0.0 was compiled successfully on an August 2026 Rust/Oxide test server following the force-wipe update.

## Changelog

See [`CHANGELOG.md`](CHANGELOG.md).

## License

SmartRadar is available under the [MIT License](LICENSE).
