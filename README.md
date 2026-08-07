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

The default aliases are `/radar`, `/sradar`, and `/smartradar`.

## Radar modes

| Mode | Displays |
| --- | --- |
| `players` | Active players and, when enabled, sleeping players |
| `stashes` | Stashes, their distance, and hidden/exposed state |
| `tcs` | Tool cupboards and their distance |
| `all` | Players, stashes, and tool cupboards |

## Commands

| Command | Description |
| --- | --- |
| `/radar` | Toggle SmartRadar using saved settings |
| `/radar on` | Enable SmartRadar |
| `/radar off` | Disable SmartRadar |
| `/radar status` | Display the active and saved settings |
| `/radar help` | Display command help |
| `/radar reset` | Restore the user's settings to defaults |
| `/radar <players\|stashes\|tcs\|all> [distance] [rate]` | Select a mode with optional distance and refresh rate |
| `/radar mode <mode>` | Change the current mode |
| `/radar distance <meters>` | Change the search distance |
| `/radar rate <seconds>` | Change the player refresh rate |
| `/radar for <seconds>` | Enable radar temporarily or apply an expiry to the current session |
| `/radar arrows [on\|off]` | Toggle player vision arrows |
| `/radar voice [on\|off]` | Toggle recent voice-activity indicators |
| `/radar sleepers [on\|off]` | Toggle sleeping players |
| `/radar vanished [on\|off]` | Toggle vanished players when permitted |
| `/radar filter name <text\|off>` | Filter players by a partial display name |
| `/radar filter team <all\|mine\|others\|solo>` | Filter players by team relationship |
| `/radar filter auth <all\|players\|staff\|moderators\|owners>` | Filter players by Rust authorization level |
| `/radar filter safezone <all\|inside\|outside>` | Filter players by safe-zone state |

Legacy Simple Radar syntax is also supported:

```text
/radar <rate> <distance> <mode>
```

All numeric input is validated. Negative, zero, NaN, infinite, out-of-range, or otherwise invalid values are rejected without replacing a working radar session.

## Permissions

| Permission | Purpose |
| --- | --- |
| `smartradar.use` | Base permission required to control SmartRadar |
| `smartradar.players` | Use player radar |
| `smartradar.stashes` | Use stash radar |
| `smartradar.cupboards` | Use tool-cupboard radar |
| `smartradar.arrows` | Display player vision arrows |
| `smartradar.voice` | Display voice-activity indicators |
| `smartradar.sleepers` | Include sleeping players |
| `smartradar.extendedrange` | Use distances beyond the standard maximum |
| `smartradar.seevanished` | Explicitly display vanished players |
| `smartradar.seeowners` | Allow a moderator to display owner-level players |

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
