# Milestone 3: UseCase Boundary Cleanup

## Goal

Unity controller が domain rule を直接知らず、UseCase / Orchestrator 経由で world を進める構造を維持する。

## Target Scope

- `CreateWorldUseCase`
- `ApplyInteractionObjectsUseCase`
- `AdvanceWorldUseCase`
- `LittlePeopleWorldOrchestrator`
- `LittlePeopleWorldController`

## Current Implementation

- Unity controller gathers input from the active provider.
- Controller calls `orchestrator.AdvanceFrame(deltaTime, interactionObjects)`.
- `World.SetInteractionObjects` rebuilds derived runtime data.
- Visual and animation synchronization remains in Unity layer.

## Acceptance Check

- Mouse input and UDP input can be switched without changing domain logic.
- `World`, `LittlePerson`, `WalkableSurface`, and `AmbientObject` remain input-source agnostic.
- New visual features can read world state without moving game rules into views.

## Handoff Notes

Particle and plant animation currently lives in Unity layer by design. It should not be moved into domain unless the behavior becomes core game logic.
