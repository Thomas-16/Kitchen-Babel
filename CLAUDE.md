# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Kitchen Babel is a cooking/time-management game built in **Unity 6000.3.8f1** using **URP (Universal Render Pipeline)**. Players pick up ingredients, process them at various counters (cut, fry), assemble plates, and deliver orders before time runs out.

## Architecture

### Singleton Managers
Core systems use a singleton pattern accessed via `.Instance`:
- `KitchenGameManager` — game state machine (WaitingToStart → CountdownToStart → GamePlaying → GameOver)
- `DeliveryManager` — spawns recipe orders every 4s (max 4), validates plate deliveries
- `GameInput` — wraps Unity's New Input System with rebindable controls
- `Player` — player movement and counter interaction via raycasts

### Event-Driven Communication
Systems are decoupled through C# events with `EventArgs`:
- Game state changes: `KitchenGameManager.OnStateChanged`
- Progress tracking: `IHasProgress.OnProgressChanged`
- Object placement: `BaseCounter.OnAnyObjectPlacedHere` (static event)
- Plate composition: `PlateKitchenObject.OnIngredientAdded`

### Counter System
All kitchen stations inherit from `BaseCounter` (abstract) and override:
- `Interact()` — primary action (pick up / place item)
- `InteractAlternate()` — secondary action (cut, cook)

Counter types: `ClearCounter`, `ContainerCounter`, `CuttingCounter`, `StoveCounter`, `PlatesCounter`, `DeliveryCounter`, `TrashCounter`. Each has companion `*Visual.cs` and sometimes `*Sound.cs` scripts.

`StoveCounter` has its own state machine: Idle → Frying → Fried → Burned.

### Key Interfaces
- `IKitchenObjectParent` — anything that can hold a `KitchenObject` (Player, counters)
- `IHasProgress` — objects with progress bars (cutting, cooking)

### ScriptableObject Data Layer
Game data is defined as ScriptableObjects in `Assets/Scriptable Objects/`:
- `KitchenObjectSO` — ingredient definition (prefab, sprite, name)
- `RecipeSO` / `RecipeListSO` — recipe definitions (list of `KitchenObjectSO`)
- `CuttingRecipeSO`, `FryingRecipeSO`, `BurningRecipeSO` — processing recipes with input/output/timer

### Input System
Uses Unity's New Input System. `PlayerInputActions` is auto-generated from the input action asset — do not edit manually. `GameInput.cs` wraps it and handles rebinding with PlayerPrefs persistence.

## Project Structure

- `Assets/Scripts/` — all gameplay code (no `.asmdef` files; single assembly)
- `Assets/Scripts/Counters/` — counter implementations and visuals
- `Assets/Scripts/ScriptableObjects/` — SO class definitions
- `Assets/Scripts/UI/` — all UI scripts (one per screen/widget)
- `Assets/Scenes/` — GameScene, MainMenuScene, LoadingScene
- `Assets/Prefabs/` — counter and kitchen object prefabs
- `Assets/Scriptable Objects/` — SO asset instances (recipes, items, audio refs)

## Build & Test

Standard Unity Editor workflow — no custom build scripts or CI pipeline. Open the project in Unity 6000.3.8f1+ and use the Editor to build/run. Test framework package is installed but no tests exist yet.
