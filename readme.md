# Supermarket Together

This repository contains the Crowd Control desktop pack and the source for a
BepInEx plugin. It does **not** include a packaged game-side release; `src` is
a development project rather than a drop-in mod folder.

## Requirements

- Supermarket Together.
- BepInEx for the game.
- Crowd Control with the **Supermarket Together** pack selected.
- For a local build, the game's managed assemblies and the plugin dependencies
  referenced by `src\BepinExExample.csproj`.

## Setup

For a prebuilt plugin, use the release/distribution intended for this pack. If
building locally:

1. Update the project references in `src\BepinExExample.csproj` so they point
   to the local game and BepInEx assemblies.
2. Build the `BepinExExample` project.
3. Install the output `CrowdControl.dll` and its required assets/dependencies
   under the game's `BepInEx\plugins\CrowdControl` directory.
4. Start Crowd Control, select Supermarket Together, then launch the game.

## Connection behavior

The BepInEx plugin connects to Crowd Control at `127.0.0.1:51337`. The source
checks for the Crowd Control process and retries its local TCP connection. The
host processes game actions; the plugin also performs mod-version checks for
connected players.

## Troubleshooting

- **The project will not build:** replace the machine-specific reference paths
  in the project file with paths to your Supermarket Together installation and
  required BepInEx dependencies.
- **No connection:** start the desktop app first and confirm that no firewall
  or other process blocks local port `51337`.
- **Multiplayer actions do not apply:** run Crowd Control on the host and make
  sure connected players use matching plugin versions.
