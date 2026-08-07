# Changelog

All notable changes to SmartRadar are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/SeesAll/SmartRadar/compare/1.1.1...HEAD
[1.1.1]: https://github.com/SeesAll/SmartRadar/compare/1.1.0...1.1.1
[1.1.0]: https://github.com/SeesAll/SmartRadar/compare/1.0.0...1.1.0
[1.0.0]: https://github.com/SeesAll/SmartRadar/releases/tag/1.0.0
