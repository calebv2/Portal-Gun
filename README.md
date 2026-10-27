# Portal Gun & Portal Doorways

A Township Tale PCVR mod with a portal gun and a placeable pair of linked doorways. Requires MelonLoader and SyncLib on the client and server.

## Installation

Download the latest release. Close the game and server, then install:

- `PortalGun-Client.dll` for the client. Rename it to `PortalGun.dll` and place it in the client's Mods folder.
- `PortalGun-Server.dll` for the server. Rename it to `PortalGun.dll` and place it in the server's Mods folder.

Replace the previous DLL rather than keeping duplicate copies. Keep your existing `modHashIds.json`. Start the server before connecting clients. Every player needs the client mod.

## Prefabs

| Name | Purpose |
| --- | --- |
| `PortalGun` | Handheld portal gun |
| `PortalDoorA` | Blue doorway |
| `PortalDoorB` | Orange doorway |

Spawn through Prefabulator or the server console:

```text
spawn "PlayerName" PortalGun
spawn "PlayerName" PortalDoorA
spawn "PlayerName" PortalDoorB
```

Place one of each doorway upright, at least 2.5 metres apart, using unit scale. The coloured plaques mark the entrance side. Both doors link automatically in both directions; no gun is required. The doorway pivot is at the opening centre, 1.2 metres above the ground.

## Portal behavior

- Separate live views for both VR eyes.
- Player passage and loose-item transfer, subject to opening size and exit clearance.
- Solid doorway frames on ATT's Ground collision layer.
- One linked doorway pair per world scene.

Both destinations must already be loaded. Portals do not cut holes through existing walls or terrain. Place doors in open space with clear entrances and exits. Recursive portal views and remote hand grabbing are not supported. Save/reload behavior and multiplayer travel need further in-game verification.

## Version 0.2.5

Corrects doorway collision-layer assignment on registration and loading. Includes the finished doorway designs and reduced-cost portal rendering: 512�512 per eye, duplicate capture prevention, and no cast shadows inside portal captures.

## Unity project

Open with Unity **2020.3.23f1**.

- Doorway prefabs: `Assets/PortalGun/Doorways/PortalDoorA.prefab` and `PortalDoorB.prefab`.
- Doorway preview: `Assets/PortalGun/Scenes/PortalDoorwaysPreview.unity`.
- Gun prefab: `Assets/PortalGun/Prefabs/PortalGun.prefab`.

Edit the final prefab assets directly. Use **Portal Gun > Export ATT Models** to rebuild the asset bundle. The game supplies native networking, interaction, and portal-pair behavior at runtime.

## Build

Game assemblies, MelonLoader, and SyncLib are external dependencies and are not included as standalone libraries. Export the Unity assets, then run:

```powershell
./NativeMod/Source/Build.ps1 -GamePath 'D:/Games/A Township Tale' -SyncLib 'D:/Mods/SyncLib.dll' -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2020.3.23f1/Editor'
```

Client and server DLLs are written to `Builds/Native`. Install the correct DLL for each role.
