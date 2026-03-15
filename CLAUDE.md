# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Kitchen Babel is a multiplayer co-op cooking/time-management game built in **Unity 6000.3.8f1** using **URP (Universal Render Pipeline)** and **Photon Fusion 2.0.9 (Host Mode)**. Up to 6 players pick up ingredients, process them at various counters (cut, fry), assemble plates, and deliver orders before time runs out.

## Architecture

### Networking (Photon Fusion 2)
Core gameplay classes extend `NetworkBehaviour`. Game logic runs on the host in `FixedUpdateNetwork()` using `Runner.DeltaTime`. State is synced via `[Networked]` properties with `ChangeDetector` in `Render()` to fire events on all clients. ScriptableObject references are sent as int indices via `KitchenObjectListSO`.

Key patterns:
- Networked singletons (`KitchenGameManager`, `DeliveryManager`, `KitchenGameMultiplayer`) set `Instance` in `Spawned()` and fire a static `OnInstanceReady` event. UI/audio scripts defer their subscriptions to this event.
- Player is a network-spawned prefab. `Player.LocalInstance` (set when `HasInputAuthority`) replaces the old `Player.Instance` singleton for local UI.
- Input is polled via `KitchenGameMultiplayer.OnInput()` into a `PlayerNetworkInput` struct.
- Kitchen objects are spawned/despawned via `KitchenGameMultiplayer.SpawnKitchenObject()` / `DestroyKitchenObject()`.
- Host pause: `KitchenGameManager.IsPaused` networked bool (no `Time.timeScale`).

### Singleton Managers
Core systems use a singleton pattern accessed via `.Instance` (set in `Spawned()` for NetworkBehaviours):
- `KitchenGameManager` — game state machine (WaitingToStart → CountdownToStart → GamePlaying → GameOver)
- `DeliveryManager` — spawns recipe orders every 4s (max 4), validates plate deliveries
- `KitchenGameMultiplayer` — handles player spawn/despawn, input polling, kitchen object lifecycle
- `GameInput` — wraps Unity's New Input System with rebindable controls (local only, not networked)

### Counter System
All kitchen stations inherit from `BaseCounter` (abstract) and override:
- `Interact()` — primary action (pick up / place item)
- `InteractAlternate()` — secondary action (cut, cook)

Counter types: `ClearCounter`, `ContainerCounter`, `CuttingCounter`, `StoveCounter`, `PlatesCounter`, `DeliveryCounter`, `TrashCounter`. Each has companion `*Visual.cs` and sometimes `*Sound.cs` scripts.

`StoveCounter` has its own state machine: Idle → Frying → Fried → Burned.

### ScriptableObject Data Layer
Game data is defined as ScriptableObjects in `Assets/Scriptable Objects/`:
- `KitchenObjectSO` — ingredient definition (prefab, sprite, name)
- `RecipeSO` / `RecipeListSO` — recipe definitions (list of `KitchenObjectSO`)
- `CuttingRecipeSO`, `FryingRecipeSO`, `BurningRecipeSO` — processing recipes with input/output/timer
- `KitchenObjectListSO` — registry mapping `KitchenObjectSO` to int indices for network serialization

### Input System
Uses Unity's New Input System. `PlayerInputActions` is auto-generated from the input action asset — do not edit manually. `GameInput.cs` wraps it and handles rebinding with PlayerPrefs persistence.

### Notes to Claude
- You can always tell me to do anything in the Unity Editor since you can't access that.