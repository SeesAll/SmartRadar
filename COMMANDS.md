# SmartRecon command reference

This document covers every player-facing command and internal UI console endpoint included with SmartRecon 2.5.1.

The default radar aliases are `/radar`, `/recon`, and `/smartrecon`. Every `/radar` example below also works with either of the other radar aliases. The default vanish aliases are `/vanish` and `/v`. Inventory inspection uses `/inv` and `/invspy`. Server owners can change these aliases in the configuration.

## Radar control

| Command | Description |
| --- | --- |
| `/radar` | Toggles radar on or off using the caller's saved mode and settings. |
| `/radar on` | Enables radar using the caller's saved mode, filters, distance, and refresh rate. |
| `/radar off` | Disables the caller's active radar session. |
| `/radar status` | Reports the active mode, layers, distance, refresh rate, filters, UI state, arrows, voice, sleepers, and temporary duration. |
| `/radar help` | Displays SmartRecon's built-in radar command summary. |
| `/radar reset` | Restores the caller's saved radar preferences to the configured defaults. If the default mode is not permitted, SmartRecon selects the first permitted mode. |

## Radar modes

| Command | Description |
| --- | --- |
| `/radar players [distance] [rate]` | Selects player mode and starts radar, optionally changing distance and refresh rate. |
| `/radar stashes [distance] [rate]` | Selects stash mode and starts radar. |
| `/radar tcs [distance] [rate]` | Selects Tool Cupboard mode and starts radar. |
| `/radar all [distance] [rate]` | Selects the combined Players, Stashes, and Tool Cupboards mode and starts radar. |
| `/radar custom [distance] [rate]` | Starts radar using the independently selected custom layers. |
| `/radar mode <players\|stashes\|tcs\|all\|custom>` | Changes the saved mode and active session mode without starting an inactive session. |

Accepted mode aliases include `player`, `stash`, `tc`, `cupboard`, `cupboards`, and their plural forms.

## Independent radar layers

Changing an independent layer switches the saved profile to `custom` mode. Omitting `[on|off]` toggles the current state.

| Command | Description |
| --- | --- |
| `/radar layer players [on\|off]` | Toggles active-player radar. |
| `/radar layer npcs [on\|off]` | Toggles supported humanoid NPC and animal radar. |
| `/radar layer loot [on\|off]` | Toggles the bounded dropped/world-loot layer. |
| `/radar layer stashes [on\|off]` | Toggles hidden and exposed stash radar. |
| `/radar layer tcs [on\|off]` | Toggles Tool Cupboard radar. `tc` and `cupboards` are accepted aliases. |

## Information toggles

Omitting `[on|off]` toggles the current state.

| Command | Description |
| --- | --- |
| `/radar arrows [on\|off]` | Toggles fixed-length player viewing-direction arrows. |
| `/radar voice [on\|off]` | Toggles indicators for players who spoke recently. |
| `/radar sleepers [on\|off]` | Toggles sleeping-player targets. |
| `/radar vanished [on\|off]` | Toggles permitted vanished-player targets. Privacy permissions still apply. |
| `/radar extended [on\|off]` | Toggles held-item and weapon-attachment information beneath player labels. |
| `/radar tclinks [on\|off]` | Toggles player-to-nearest-authorized-TC arrows and TC authorization counts. |

Toggle commands accept `on`, `true`, or `1` for enabled; `off`, `false`, or `0` for disabled; and `toggle` to invert the current state.

## Distance, refresh rate, and duration

| Command | Description |
| --- | --- |
| `/radar distance <meters>` | Changes the saved and active radar distance, subject to the caller's permitted maximum. |
| `/radar rate <seconds>` | Changes the saved and active player refresh interval within configured limits. |
| `/radar for <seconds>` | Starts radar if necessary and automatically stops it after the specified duration. |

All numeric input must be positive and finite. Out-of-range input is rejected without replacing a working radar session.

## Player filters

| Command | Description |
| --- | --- |
| `/radar filter name <text>` | Shows only players whose display names contain the supplied text. Multiple words are supported. |
| `/radar filter name off` | Clears the name filter. |
| `/radar filter team all` | Shows players regardless of current Rust-team relationship. |
| `/radar filter team mine` | Shows only members of the viewer's current Rust team. |
| `/radar filter team others` | Shows players outside the viewer's current Rust team. |
| `/radar filter team solo` | Shows players who have no current Rust team. |
| `/radar filter auth all` | Shows every permitted Rust authorization level. |
| `/radar filter auth players` | Shows regular players only. |
| `/radar filter auth staff` | Shows moderators and owners. |
| `/radar filter auth moderators` | Shows moderators only. |
| `/radar filter auth owners` | Shows owners only. |
| `/radar filter safezone all` | Shows players both inside and outside safe zones. |
| `/radar filter safezone inside` | Shows only players inside safe zones. |
| `/radar filter safezone outside` | Shows only players outside safe zones. |

Accepted filter aliases are:

- `team` for `mine`
- `other` for `others`
- `noteam` for `solo`
- `player` for `players`
- `admins` for `staff`
- `moderator` or `mods` for `moderators`
- `owner` for `owners`
- `in` for `inside`
- `out` for `outside`

## Forensic searches

| Command | Description |
| --- | --- |
| `/radar findid <SteamID>` | Draws up to 250 entities associated with the Steam ID through ownership, TC authorization, sleeping-bag assignment, or code-lock authorization. Results remain visible for 30 seconds. |
| `/radar buildings` | Draws up to 250 twig building blocks for 30 seconds; `twig` is the default filter. |
| `/radar buildings twig` | Explicitly performs the default twig-building search. |
| `/radar buildings unprivileged` | Draws up to 250 building blocks without building privilege for 30 seconds. |
| `/radar drops` | Draws indexed nearby dropped/world loot for 30 seconds using the saved radar distance. |
| `/radar drops <distance>` | Performs the same indexed loot search using the supplied distance. |

Forensic searches are permission-gated, time-sliced, capped at 250 drawings, and protected by a ten-second cooldown.

## Investigation panel

| Command | Description |
| --- | --- |
| `/radar ui` | Shows or hides the right-side investigation panel. |
| `/radar ui move` | Opens the centered panel-positioning controller with directional arrows, 1%, 5%, and 10% movement steps, live preview, Reset, Cancel, and Save. Radar must be active. |
| `/radar ui reset` | Deletes the caller's saved panel position and restores the server-configured default anchors. |

## Vanish

Every `/vanish` example also works with `/v` by default.

| Command | Description |
| --- | --- |
| `/vanish` | Toggles SmartRecon's built-in vanish state. |
| `/vanish on` | Enters vanish, enables configured noclip and protections, and starts the configured investigative radar profile. |
| `/vanish off` | Leaves vanish and normally stops the vanish-started radar session. Permanent-vanish users cannot turn vanish off. |
| `/vanish status` | Reports current vanish, radar, and vision-arrow states. |
| `/vanish help` | Displays SmartRecon's built-in vanish command summary. |

`/vanish true` and `/vanish 1` are accepted in place of `/vanish on`. `/vanish false` and `/vanish 0` are accepted in place of `/vanish off`.

## Inventory inspection

Every `/inv` example also works with `/invspy` by default.

| Command | Description |
| --- | --- |
| `/inv <player name>` | Opens the matched active or sleeping player's main, clothing, and belt inventories. Multi-word names are supported. |
| `/inv <SteamID>` | Opens the specified active or sleeping player's inventory. |
| `/inv` | Attempts to inspect the nearby player the administrator is directly looking at. |

## Action-driven features

These features do not require a chat command after their permissions are granted:

| Action | Description |
| --- | --- |
| Vanished hammer strike | Privately inspects the struck entity's ownership or type-specific authorization without repairing, upgrading, or damaging it. |
| Qualifying TC or turret strike | Opens the centered authorization popup when the configured shared-authorization threshold is met. |
| Direct code-lock strike | Inspects the lock whitelist and guest authorization; qualifying lists open the centered popup. Actual codes are never shown. |
| Vanished map right-click | Places a temporary marker, teleports the administrator to it, and removes the marker. Visible players and native spectators are ignored. |
| Reload while aiming at a supported target | Performs permitted inventory, door, lock-bypass, or vehicle interaction while vanished. |

## Internal UI console endpoints

The following commands are generated by SmartRecon's CUI buttons. They are documented for completeness but are not intended as normal chat commands.

| Console command | Description |
| --- | --- |
| `smartrecon.ui close` | Closes the investigation panel. |
| `smartrecon.ui <players\|npcs\|loot\|stashes\|tcs\|sleepers\|vision\|extended\|tclinks\|voice>` | Handles the corresponding investigation-panel toggle. |
| `smartrecon.uimove <up\|down\|left\|right>` | Moves the investigation panel during an active positioning session. |
| `smartrecon.uimove step <1\|5\|10>` | Selects the movement increment. |
| `smartrecon.uimove reset` | Previews the server-configured default position. |
| `smartrecon.uimove cancel` | Cancels the positioning session without saving. |
| `smartrecon.uimove save` | Saves the previewed position for that administrator. |
| `smartrecon.inspectui close` | Closes the centered authorization report. |
| `smartrecon.inspectui previous` | Opens the previous authorization-report page. |
| `smartrecon.inspectui next` | Opens the next authorization-report page. |

## Permission reminder

`smartrecon.use` grants access to the radar command family but does not grant every radar category. Vanish, inventory inspection, map teleportation, hammer inspection, UI operation, forensic searches, extended range, and privacy-sensitive target visibility have separate permissions. See the complete permission table in [`README.md`](README.md#permissions).
