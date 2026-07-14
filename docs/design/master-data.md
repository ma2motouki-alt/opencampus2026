# Master Data Design

Master data is immutable while the app is running. Domain objects hold runtime state and reference master records by id.

## Naming

- `~Master`: immutable definition record.
- `~Table`: in-memory table of master records.
- `~Instance`: runtime object created from or referencing a master.

## Current Master Tables

| Master | Purpose |
|---|---|
| `WorldPresetMaster` | Initial little-person count, background color, and environment theme. |
| `LittlePersonArchetypeMaster` | Little-person color, size, speed, curiosity, and fear. |
| `BehaviorProfileMaster` | Edge-walk timing and personality tuning. |
| `ReactionMaster` | Runtime reaction kinds. |
| `ReactionConditionMaster` | Reaction conditions. |
| `InteractionObjectTypeMaster` | Hand, round prop, development mask stroke, and block prop defaults. |
| `InteractionFieldMaster` | Repel, curiosity, and legacy field definitions. |
| `RainbowMaster` | Rainbow trigger, lifetime, geometry, walking, and touch tuning. |
| `RainbowCloudJumpMaster` | Rainbow-to-cloud search, jump, contact, return, and reservation tuning. |
| `AmbientObjectTypeMaster` | Cloud and star size, drift, touch radius, movement band, and effect link. |
| `VisualEffectMaster` | Procedural or prefab visual effect definitions. |
| `SoundCueMaster` | Placeholder for future audio cues. |
| `TuningParameterMaster` | Global runtime tuning. |

## Important Current Values

### World

- Preset id: `1`
- Little people count: `20`
- Background: dark table color

### Cloud

Cloud master:

- Size: `0.095 x 0.05`
- Drift: `0.014 x 0.003`
- Contact radius: `0.075`
- Visual effect: `RainColumn`
- Movement edge padding: `0.18`
- Max center Y: `0.38`

The movement band keeps clouds away from ordinary edge-walk routes.

### Star

- Display role: fixed sun
- Size: `0.08 x 0.08`
- Drift: `0 x 0`
- Contact radius: `0.085`
- Visual effect: `StarBurst`

### Rain

`RainColumn` master:

- Pulse speed: `4.0`
- Alpha: `0.72`
- Default size: `0.075 x 0.28`
- Duration: `0.45`
- Drop size scale: `0.4`

### Rainbow Cloud Jump

- Search distance: `0.16`
- Cloud touch dwell: `0.22 seconds`
- Jump arc height: `0.07`
- Return arc height: `0.025`
- Maximum jumpers per cloud: `1`
- Existing raining clouds are excluded from new jump targets.

### Global Tuning

Important values:

- `WorldEdgePadding = 0.03`
- `InputHitPadding = 0.02`
- `HandContourReactionPadding = 0.035`
- `FallDuration = 0.72`
- `AmbientCloudCount = 3`
- `AmbientStarCount = 1`
- `RainLingerSeconds = 2.0`
- `StarCooldownSeconds = 1.4`
- `SurfaceReconnectCooldownSeconds = 1.1`

## Externalization

Masters are still C# immutable objects and in-memory tables. SQLite, MasterMemory, ScriptableObject conversion, or CSV import are deferred.
