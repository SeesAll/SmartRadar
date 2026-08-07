# Changelog

All notable changes to SmartRecon are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.1.0] - 2026-08-07

### Added

- Native spectating now gives the SmartRecon panel its own cursor, allowing every permitted UI toggle to be used without an inventory cursor.
- Spectating UI status and instructions now reflect the spectator workflow; closing the panel releases the cursor and `/radar ui` reopens it.

### Fixed

- NPC and Loot UI changes now switch the saved profile to `custom`, matching every other independent layer control.
- `/radar ui` now toggles the panel's actual runtime visibility, including when automatic panel display is disabled.
- Sleeper indexing now remains active when the Players layer is temporarily forced for a spectating session.
- `IsRadarLayerEnabled` now reports the effective forced Players layer during spectating.
- Panels retained after spectating ends are rebuilt without spectator cursor ownership.
- Scheduler-driven session expiration or permission cleanup now emits the normal radar-deactivation hook.
- Pooled network-subscriber lists are now returned safely even if Rust throws while a player enters vanish.

### Performance

- Added fair rotating session scheduling so administrators cannot be starved when more radar sessions are due than the configured per-tick limit.
- Added adaptive spatial traversal that scans occupied cells instead of large empty coordinate ranges when that is cheaper.
- Reduced vanished-player metabolism maintenance from every physics frame to a bounded four times per second while retaining protection.
- Removed disconnected-user forensic cooldown entries and unused mode helper methods.

## [2.0.6] - 2026-08-07

### Changed

- Removed the unused pre-release name-migration layer so SmartRecon now has one plugin identity, one data/config identity, and one `smartrecon.*` permission namespace.
- Removed obsolete command aliases, UI console actions, lifecycle hooks, config/data imports, and numeric command syntax retained solely for pre-release compatibility.
- Permission registration and authorization checks now operate exclusively on SmartRecon permissions, eliminating Oxide plugin-prefix warnings.

## [2.0.5] - 2026-08-07

### Fixed

- Added a spectator-safe world-space drawing path for player labels, vision arrows, and TC-link arrows, avoiding entity parenting and distance fading that can cull drawings relative to the inactive spectator body.
- Added a lightweight twice-per-second spectate-state reconciliation pass so automatic radar activation and cleanup do not depend solely on hook timing.
- Spectating administrators with an existing radar session now receive the same temporary Players-layer and vision-arrow defaults without restarting their saved profile.
- The watched player remains in spectate radar results so their own label and viewing-direction arrow are visible even when no other players are nearby.
- Forced spectate Players-layer state is now honored by candidate filtering even when the administrator's saved radar profile had Players disabled.
- Saved player filters no longer suppress the watched player's own label and vision arrow during native spectating; nearby targets continue respecting those filters.
- All other radar categories continue using their existing world-space drawings centered on the watched player.

## [2.0.4] - 2026-08-07

### Added

- Native Rust spectating now starts radar automatically by default and forces player vision arrows on for the spectating session.
- Independent configuration controls for automatic spectate radar, forced spectate vision arrows, and stopping the automatically started session when spectating ends.
- Automatic investigation sessions can temporarily force the player layer when required for vision arrows without overwriting the administrator's saved profile.

### Fixed

- Entering native spectating now cleanly exits SmartRecon's limited-networking vanish state before Rust installs its spectator networking state.
- Prevented the conflicting networking modes that could leave radar apparently enabled but unable to draw while spectating.
- Spectate lifecycle hooks now remain available independently of whether any administrator is currently vanished.
- Manually running `/radar on` during native spectating now applies the same forced player-layer and vision-arrow defaults as automatic activation.

## [2.0.3] - 2026-08-07

### Fixed

- Radar sessions now follow Rust's current public spectating target while an administrator uses native spectating.
- Player, NPC, loot, stash, and tool-cupboard distance queries are centered on the watched player instead of the administrator's inactive spectator body.
- Vanish-only map-marker teleport remains disabled throughout native spectating.

## [2.0.2] - 2026-08-07

### Fixed

- Player vision arrows now draw directly from the authoritative world-space eye ray instead of converting it through the player-root transform.
- Corrected vision arrows that could appear behind a player and indicate the opposite of their actual viewing direction.

## [2.0.1] - 2026-08-07

### Fixed

- Corrected the configuration load order so Oxide initializes its configuration object before SmartRecon reads it.
- Prevented the resulting `NullReferenceException` that could stop SmartRecon from initializing.

## [2.0.0] - 2026-08-07

### Changed

- Established SmartRecon as the identity for the complete investigation suite.
- Plugin source, class, UI branding, configuration example, GitHub repository, release assets, documentation, and audit-log identity use SmartRecon.
- Permissions use the `smartrecon.*` namespace.

### Added

- `/recon` and `/smartrecon` command aliases alongside `/radar`.
- `OnSmartReconActivated` and `OnSmartReconDeactivated` hooks for integrations.

## [1.3.1] - 2026-08-07

### Added

- Optional separate-file audit logging for successful vanished map-marker teleports, enabled by default.
- Audit entries include UTC timestamp, administrator name, Steam ID, origin, and destination coordinates.

### Changed

- Native Rust admin spectating now explicitly disables map-marker teleport processing. SmartRecon leaves the note and spectating session untouched.

## [1.3.0] - 2026-08-07

### Added

- Vanish-only map-marker teleport integrated directly into SmartRecon under the `smartrecon.vanish.teleport` permission.
- Collision-aware destination height selection covering terrain, structures, water, and large vehicles.
- Configurable landing offset, optional preservation of a higher noclip altitude, automatic used-marker removal, and a small anti-double-fire interval.

### Changed

- Vanished administrators no longer need to hold reload while placing the map marker. Placing the note is the teleport action.
- Map-marker teleport is enabled by default but remains protected by its dedicated permission.
- Visible players are never intercepted; their map notes retain ordinary Rust behavior.

### Performance

- The marker hook remains dynamically subscribed only while at least one SmartRecon-managed administrator is vanished.
- Teleport processing performs one bounded raycast and constant-time state checks only when an eligible marker is placed.

## [1.2.3] - 2026-08-06

### Fixed

- Humanoid NPCs are now registered from entity spawn/kill events and the initial server-entity pass. Rust does not reliably place scientists and other server-controlled humanoids in `BasePlayer.activePlayerList`, which prevented the previous NPC detection from drawing them.
- Humanoid NPCs are excluded from the real-player spatial index to prevent duplicate labels on NPC types that do appear in both collections.

### Changed

- Humanoid NPC labels use their display name when available and the player-height label offset.

## [1.2.2] - 2026-08-06

### Fixed

- The NPC layer now includes animals and other non-player NPC entities instead of recognizing only player-shaped `NPCPlayer` instances.
- Humanoid NPC detection now recognizes non-Steam Rust NPC identities, improving coverage for scientists, dwellers, guards, scarecrows, and plugin-created NPC players.

### Added

- Moving spatial tracking and labels for older and newer Rust animal/NPC entity bases, farmable animals, wildlife hazards, sharks, horses, and travelling vendors.
- A separate configurable NPC/animal result limit and drawing color.

### Performance

- Animals are registered through spawn/kill hooks and only the bounded tracked-NPC collection is reindexed with moving targets; SmartRecon does not scan all server entities every player refresh.

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
- Mode presets now populate independent layers while panel or layer-command changes switch the profile to `custom`.
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
- SmartRecon does not require a separate Vanish plugin for invisibility.
- Configuration, data storage, permissions, commands, and documentation now cover both radar and vanish.

### Performance

- Vanish protection hooks are dynamically subscribed only while at least one administrator is vanished.
- Damage, lock, marker, and anti-hack hooks are individually gated by configuration.
- Harmony prefixes exit immediately when no SmartRecon-managed administrator is vanished.

### Security

- Incoming damage is blocked for vanished investigators by default.
- Outgoing damage is blocked unless the investigator has `smartrecon.vanish.damage`.
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
- Granular feature and privacy permissions.
- Optional Vanish API integration with a limited-networking fallback.
- Default protection for vanished players and owner-level administrators.
- Dynamic voice-hook subscription and idle player-index suspension.
- Configuration validation, localization, safe lifecycle cleanup, and corrupt-data recovery.

[Unreleased]: https://github.com/SeesAll/SmartRecon/compare/2.1.0...HEAD
[2.1.0]: https://github.com/SeesAll/SmartRecon/compare/2.0.6...2.1.0
[2.0.6]: https://github.com/SeesAll/SmartRecon/compare/2.0.5...2.0.6
[2.0.5]: https://github.com/SeesAll/SmartRecon/compare/2.0.4...2.0.5
[2.0.4]: https://github.com/SeesAll/SmartRecon/compare/2.0.3...2.0.4
[2.0.3]: https://github.com/SeesAll/SmartRecon/compare/2.0.2...2.0.3
[2.0.2]: https://github.com/SeesAll/SmartRecon/compare/2.0.1...2.0.2
[2.0.1]: https://github.com/SeesAll/SmartRecon/compare/2.0.0...2.0.1
[2.0.0]: https://github.com/SeesAll/SmartRecon/compare/1.3.1...2.0.0
[1.3.1]: https://github.com/SeesAll/SmartRecon/compare/1.3.0...1.3.1
[1.3.0]: https://github.com/SeesAll/SmartRecon/compare/1.2.3...1.3.0
[1.2.3]: https://github.com/SeesAll/SmartRecon/compare/1.2.2...1.2.3
[1.2.2]: https://github.com/SeesAll/SmartRecon/compare/1.2.1...1.2.2
[1.2.1]: https://github.com/SeesAll/SmartRecon/compare/1.2.0...1.2.1
[1.2.0]: https://github.com/SeesAll/SmartRecon/compare/1.1.1...1.2.0
[1.1.1]: https://github.com/SeesAll/SmartRecon/compare/1.1.0...1.1.1
[1.1.0]: https://github.com/SeesAll/SmartRecon/compare/1.0.0...1.1.0
[1.0.0]: https://github.com/SeesAll/SmartRecon/releases/tag/1.0.0
