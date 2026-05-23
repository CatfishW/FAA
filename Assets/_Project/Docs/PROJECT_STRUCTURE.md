# FAA Unity Project Structure

This project keeps first-party work under `Assets/_Project` and leaves vendor packages at the top level.

## Main Folders

- `Assets/_Project/Scenes`: FAA-owned scenes. `ExperimentScene.unity` is the working X-Plane/HUD scene.
- `Assets/_Project/Scripts`: FAA-owned runtime and editor code.
- `Assets/_Project/Scripts/Editor`: Unity editor automation, diagnostics, setup, and hierarchy tools.
- `Assets/_Project/Scripts/XPlaneIntegration`: X-Plane API, MQTT, terrain sync, and bridge code.
- `Assets/_Project/Scripts/HUDControl`: uGUI HUDControl implementation. This is the default HUD.
- `Assets/_Project/Scripts/HUDToolkit`: FAA HUD mode switcher and the secondary UI Toolkit HUD.
- `Assets/_Project/Scripts/Customization`: HUD color/customization runtime safety code.
- `Assets/_Project/Materials`, `Textures`, `Prefabs`, `ScriptableObjects`, `Shaders`: first-party assets grouped by type.
- `Assets/_Project/Docs`: project notes and integration documentation.

## Vendor And Legacy Folders

- `Assets/ThirdParty`, `Assets/Plugins`, `Assets/UniStorm 3.0`, `Assets/TextMesh Pro`, and `Assets/Radial Menu Framework` are external or vendor-owned.
- Avoid moving vendor content unless replacing or upgrading the package.
- `Assets/Screenshots` contains tracked historical visual evidence. New FAA validation screenshots are ignored and should live under `_artifacts/screenshots`.
- `Archive/LegacyScenes` keeps old and recovery scene files versioned outside `Assets`, so Unity does not import stale scene references while programmers work in the active project.

## Active Scene Hierarchy

Run `FAA/Scene/Organize Experiment Scene Hierarchy` after large scene edits. It preserves functional object names while grouping the scene as:

- `FAA_Scene`
- `_Systems`: managers, voice, visual understanding, geo projection, and service objects.
- `_Gameplay`: ownship and gameplay objects.
- `_UI`: uGUI HUD, UI Toolkit HUD alternative, and UI overlays.
- `_Environment`: weather, maps, and lighting groups.

Do not rename these functional objects without checking code references: `_UI`, `[MANAGERS]`, `FAASymbologyCanvas`, `HUDController`, `OwnAircraft`, `WeatherVisualization3D`, `UniStorm System`, `OnlineMap`, and `GeoPosUnityPosProjectManager`.

## Runtime Artifacts

Generated screenshots, local browser captures, Steam/X-Plane smoke-test images, and test output are ignored under `_artifacts`. Keep reproducible source assets in `Assets/_Project`; keep temporary evidence outside `Assets` so Unity does not import it.
