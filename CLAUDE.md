# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Kitchen Babel is a multiplayer co-op cooking/time-management game built in **Unity 6000.3.8f1** using **URP (Universal Render Pipeline)** and **Photon Fusion 2.0.9 (Host Mode)**. Players pick up ingredients, process them at various counters (cut, fry), assemble plates, and deliver orders before time runs out.

## Architecture

- **Networking**: Photon Fusion 2 Host Mode. Core classes extend `NetworkBehaviour`, logic runs in `FixedUpdateNetwork()`, state synced via `[Networked]` properties. ScriptableObject refs sent as int indices via `KitchenObjectListSO`.
- **Singletons**: `KitchenGameManager`, `DeliveryManager`, `KitchenGameMultiplayer` set `Instance` in `Spawned()` and fire `OnInstanceReady`. `GameInput` wraps the New Input System (local only).
- **Counters**: All inherit `BaseCounter`, override `Interact()` and `InteractAlternate()`. Each has companion `*Visual.cs`/`*Sound.cs`.
- **Data**: ScriptableObjects in `Assets/Scriptable Objects/` — `KitchenObjectSO`, `RecipeSO`, `CuttingRecipeSO`, `FryingRecipeSO`, `BurningRecipeSO`.
- **Input**: `PlayerInputActions` is auto-generated — don't edit manually. `GameInput.cs` handles rebinding.

### Notes to Claude
- You can always tell the user to do anything in the Unity Editor like creating GameObjects/Prefabs, adding compoonents, etc.
- You can use the Unity MCP.
- Asking the user clarifying questions and design choice questions with the ask question tool is a great idea before planning.