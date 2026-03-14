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

### Input System
Uses Unity's New Input System. `PlayerInputActions` is auto-generated from the input action asset — do not edit manually. `GameInput.cs` wraps it and handles rebinding with PlayerPrefs persistence.

### Notes to Claude
- You can always tell me to do anything in the Unity Editor since you can't access that.