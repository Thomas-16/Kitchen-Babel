# Kitchen Babel

A multiplayer co-op / versus cooking and time-management game built in Unity. Pick up ingredients, process them at counters (cut, fry, plate), and ship orders before the clock — or the other team — beats you to it.

- **Engine:** Unity 6000.3.8f1 (URP 17.3)
- **Networking:** Photon Fusion 2 (Host Mode)
- **Input:** Unity Input System
- **Targets:** Windows, WebGL
- **Version:** 0.1.0

## Game modes

- **Co-op** — players share a kitchen and a deliveries queue, racing the clock to ship recipes.
- **Versus** — two teams compete; matches have no time limit and end when one team runs out of hearts.

## Getting started

1. Clone the repo.
2. Open the project folder in Unity Hub with Unity 6000.3.8f1.
3. Configure your **Photon Fusion** App ID in `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`.
4. Open `Assets/Scenes/MainMenuScene.unity` and press Play.

## Third-party assets

- **Photon Fusion 2** (2.0.9) — networking
- **Heat – Complete Modern UI** — UI kit
- **MAD Easy Physics Conveyor System** — conveyor counters
