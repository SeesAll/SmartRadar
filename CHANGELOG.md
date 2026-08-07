# Changelog

All notable changes to SmartRadar are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/SeesAll/SmartRadar/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/SeesAll/SmartRadar/releases/tag/v1.0.0
