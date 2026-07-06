# Master Data Design

Master data is immutable while the app is running. Domain objects hold runtime state and reference master records by id.

## Naming

- `~Master`: one immutable master record.
- `~Table`: in-memory table of master records.
- `~Spec`: immutable composition of multiple masters for runtime convenience.
- `~Instance`: runtime object created from a master.

## MVP Masters

| Master | Purpose |
|---|---|
| `WorldPresetMaster` | Initial little-person count, background color, and environment theme. |
| `LittlePersonArchetypeMaster` | Little-person color, size, speed, curiosity, and fear. |
| `BehaviorProfileMaster` | Edge-walk turn timing and lightweight personality tuning. |
| `ReactionMaster` | Runtime reactions such as startle, curiosity, and surface walk. |
| `ReactionConditionMaster` | Trigger distance and input object constraints. |
| `InteractionObjectTypeMaster` | Hand, round prop, bar prop, and default dimensions. |
| `InteractionFieldMaster` | Non-walkable influence fields such as repel, curiosity, and guide-edge legacy data. |
| `WalkableSurfaceMaster` | Attach distance, detach distance, surface speed, ride velocity limit, debug/internal surface width, transfer time, attach/exit insets, surface-to-surface connection tuning, vertical-angle tolerance, and bar obstacle response tuning. |
| `AmbientObjectTypeMaster` | Cloud and star defaults such as size, drift velocity, touch radius, and linked visual effect. |
| `VisualEffectMaster` | Visual effect kind, render mode, optional prefab key, size, duration, color, alpha, and procedural tuning. |
| `SoundCueMaster` | Placeholder for future Unity Audio cues. |
| `TuningParameterMaster` | Global values such as max delta time, edge padding, fall arc, ambient counts, and surface reconnect cooldown. |

## Walkable Surface Masters

`WalkableSurfaceMaster` configures how little people attach to and move on prop-derived paths.

- `AttachDistance`: maximum distance from a little person to a surface attach point for transfer.
- `DetachDistance`: maximum tolerated per-frame jump before falling.
- `SurfaceWalkSpeed`: normalized-distance speed along the surface.
- `RideVelocityLimit`: maximum source prop velocity before falling.
- `SurfaceWidth`: debug line width and internal surface tolerance. It is not used to choose the walking position; bar surfaces are generated from the real rectangle edge.
- `BarVisualScale`: scale factor used by the Unity bar renderer. The current bar uses `InteractionObjectView`'s `1.08` edge scale on a square sprite whose bounds are `4` Unity units, so the default is `4.32`. Surface edges and endpoints should use this so they align with the visible cyan rectangle.
- `TransferDurationSeconds`: time spent moving from edge to surface.
- `AttachProgressInset`: progress inset from the surface start used for the attach point. Keep this small for Milestone 2, around `0.03`, so little people board near the bar end and still walk almost the full bar length.
- `ExitProgressInset`: progress inset from the surface end used for the exit point. Milestone 2 should default this to `0.0` so little people fall from the center-side corner of the walked rectangle edge.
- `SurfaceExitDwellSeconds`: short time to keep a little person at the walked edge corner before trying surface-to-surface transfer or falling, making the exit visible.
- `SurfaceConnectionDistance`: maximum distance from the current surface `PathEndPoint` to the closest valid point on another surface for direct prop-to-prop transfer.
- `SurfaceConnectionTransferDurationSeconds`: transition duration used when moving directly from one surface tip to another surface point.
- `SurfaceConnectionCooldownSeconds`: cooldown after a surface-to-surface transfer that prevents immediately moving back to the previous source surface.
- `TipCrossDurationSeconds`: legacy tuning from the tip-cross model. Milestone 2 does not use it.
- `ExitOppositeSidePadding`: legacy tuning from the opposite-side exit model. Milestone 2 does not use it.
- `TwoSidedVerticalToleranceDegrees`: angle tolerance around vertical where both sides of the bar can be boarded.
- `AttachSideTolerance`: tolerance for deciding whether a little person approaches from the walkable side of a surface.
- `BarObstaclePadding`: extra normalized width added to the bar's rectangular blocking area. It should not create rounded end caps beyond the visible bar tips.
- `EdgeBlockBackoffDistance`: distance used to move an edge walker slightly away after hitting a non-walkable bar side.
- `EdgeBlockCooldownSeconds`: cooldown that prevents repeated direction flips against the same bar.
- `MinWalkDistance`, `MaxWalkDistance`: legacy values from the planned-detach model. Milestone 2 should stop using them for bar-prop surface walking.

## Visual Effect Masters

`VisualEffectMaster` is the visual replacement boundary.

- `Kind`: semantic effect kind such as `RainColumn` or `StarBurst`.
- `RenderMode`: `Procedural` or `Prefab`.
- `AssetKey`: Resources prefab key used when `RenderMode = Prefab`.
- `DefaultSize`: normalized default display size.
- `DurationSeconds`: effect lifetime.
- `Color`, `PulseSpeed`, `Alpha`: procedural renderer tuning.

The MVP keeps all masters as C# immutable classes with default in-memory tables. External data files are deferred.
