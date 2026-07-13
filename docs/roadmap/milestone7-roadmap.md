# Milestone 7: UDP Input Boundary

## Goal

Unity側の入力差し替え口を完成させ、Mouse と UDP RealSense を同じ domain logic に流せるようにする。

## Target Scope

- `IInteractionInputProvider`
- `MouseInputProviderBehaviour`
- `UdpRealSenseInputProviderBehaviour`
- `LittlePeopleWorldController`
- `input-protocol.md`

## Current Implementation

- `UdpRealSenseInputProviderBehaviour` exists.
- It parses frame-level JSON.
- It supports `kind`, `shape`, and `points`.
- It clears objects after timeout.
- `LittlePeopleWorldController` can use mouse or UDP provider.

## Acceptance Check

- Mouse input still works.
- UDP test sender can create objects in Unity.
- Contour hands are parsed and displayed.
- Legacy `bar_prop` input is safely treated as hand input.
- Invalid or missing UDP packets do not stop Unity.

## Handoff Notes

The protocol is stable enough for team development. Add new fields only with backward compatibility.
