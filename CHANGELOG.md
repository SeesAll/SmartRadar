# Changelog

All notable changes to SmartRadar are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.1] - 2026-08-06

### Added

- Configurable private vanish and reappear feedback sounds, with an opt-in option to make them audible to nearby players.

### Fixed

- Vanish reload recovery now rechecks noclip immediately, on the next tick, and after the unload/reload command race has settled. This prevents a persisted administrator from retaining the invisibility indicator while being left grounded without noclip.
- Permanent vanish now requires a direct per-user permission grant. Administrator bypass, inherited group permissions, and wildcard-style staff grants can no longer accidentally lock an administrator in vanish.

### Documentation

- Clarified that Rust-authenticated moderators and owners bypass ordinary permissions by default, while regular players never do.

## [1.2.0] - 2026-08-06

### Added

- Modern right-side investigation panel with live toggles for players, NPCs, loot, stashes, tool cupboards, sleepers, vision arrows, extended information, TC links, and voice indicators.
- Independent persistent radar layers and a `custom` profile mode.
- `/radar ui` and `/radar layer <players|npcs|loot|stashes|tcs> [on|off]` commands.
- Bounded NPC and dropped/world-loot tracking with separate permissions, defaults, colors, and result limits.
- Optional held-item and weapon-attachment details for player labels.
- Optional TC authorization counts and nearest authorized player-to-TC arrows.
- Time-sliced `/radar findid` and `/radar buildings` forensic searches plus indexed `/radar drops` drawings, capped at 250 results and protected by a cooldown.
- Radar API methods and activation, deactivation, investigation-start, and investigation-end hooks.

### Changed

- Vanish-started radar now opens the investigation panel by default after forcing player vision arrows on.
- Legacy mode presets now populate independent layers while panel or layer-command changes switch the profile to `custom`.
- Radar status and help output now cover layers, UI, extended details, TC links, and forensic commands.

### Performance

- Loot uses the same shared spatial-cell strategy as stashes and tool cupboards and respects both category and total drawing limits.
- The investigation panel updates only after a setting changes rather than during radar refresh cycles.
- Forensic entity processing yields every 200 inspected entities and never emits more than 250 drawings per search.

### Security

- NPCs, loot, extended player details, TC information, UI access, and forensic searches each have separate permissions.
- Forensic searches have a ten-second per-user cooldown.

## [1.1.1] - 2026-08-06

### Fixed

- Added a cached compatibility wrapper for both known Rust `Networkable.UpdateGroups` signatures, fixing compilation on current servers that require an `EntityNetworkRange` argument while retaining compatibility with older assemblies.

## [1.1.0] - 2026-08-06

### Added

- Self-contained administrator vanish using Rust limited networking, subscriber removal, collider suppression, AI exclusion, and moving network-group updates.
- `/vanish` and `/v` commands with toggle, explicit on/off, status, and help actions.
- `/inv` and `/invspy` player inventory inspection by name, Steam ID, or nearby aimed target.
- Permission-controlled reload-key interaction with players, containers, doors, and vehicles.
- Optional reload plus map-marker teleport.
- Persisted vanish state and an explicit permanent-vanish permission.
- Vanish-compatible API methods and `OnVanishDisappear` / `OnVanishReappear` hooks.
- Harmony isolation for vanished-player signals and effects while preserving nearby effects for the investigator.

### Changed

- Entering vanish now starts the configured investigative radar profile by default.
- Vision arrows are forced on for vanish-started radar sessions without overwriting saved preferences.
- Reappearing now stops the active radar session by default.
- SmartRadar no longer requires a separate Vanish plugin for invisibility.
- Configuration, data storage, permissions, commands, and documentation now cover both radar and vanish.

### Performance

- Vanish protection hooks are dynamically subscribed only while at least one administrator is vanished.
- Damage, lock, marker, and anti-hack hooks are individually gated by configuration.
- Harmony prefixes exit immediately when no SmartRadar-managed administrator is vanished.

### Security

- Incoming damage is blocked for vanished investigators by default.
- Outgoing damage is blocked unless the investigator has `smartradar.vanish.damage`.
- Lock bypass, inventory inspection, map teleport, permanent vanish, and vanished-target visibility use separate permissions.

## [1.0.0] - 2026-08-06

### Added

- Initial public release.
- Oxide and Carbon-compatible RustPlugin implementation.
- Player, sleeping-player, stash, and tool-cupboard radar modes.
- Combined `all` mode.
- Shared spatial indexing and centralized staggered scheduling.
- Independent moving-player and static-entity refresh timing.
- Configurable per-category result limits and total drawing budget.
- Nearest-result prioritization using squared-distance comparisons.
- Entity-parented player labels and world-space static labels.
- Fixed-length player vision arrows without infinite raycasts.
- Player health, distance, team, state, authorization, safe-zone, voice, and vanish indicators.
- Player-name, team, authorization, and safe-zone filters.
- Persistent per-administrator preferences.
- Temporary radar sessions with automatic expiry.
- Legacy `/radar <rate> <distance> <mode>` command compatibility.
- Granular feature and privacy permissions.
- Optional Vanish API integration with a limited-networking fallback.
- Default protection for vanished players and owner-level administrators.
- Dynamic voice-hook subscription and idle player-index suspension.
- Configuration validation, localization, safe lifecycle cleanup, and corrupt-data recovery.

[Unreleased]: https://github.com/SeesAll/SmartRadar/compare/1.2.1...HEAD
[1.2.1]: https://github.com/SeesAll/SmartRadar/compare/1.2.0...1.2.1
[1.2.0]: https://github.com/SeesAll/SmartRadar/compare/1.1.1...1.2.0
[1.1.1]: https://github.com/SeesAll/SmartRadar/compare/1.1.0...1.1.1
[1.1.0]: https://github.com/SeesAll/SmartRadar/compare/1.0.0...1.1.0
[1.0.0]: https://github.com/SeesAll/SmartRadar/releases/tag/1.0.0
