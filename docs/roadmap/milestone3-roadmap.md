# Milestone 3: UseCase Boundary Cleanup

## Goal

現在 domain 内に寄っている処理を、テストしやすい UseCase 境界として整理する。

## Target Scope

- `CreateWorldUseCase`
- `ApplyInteractionObjectsUseCase`
- `AdvanceWorldUseCase`
- `LittlePeopleWorldOrchestrator`
- `World.SetInteractionObjects`

対象外:

- 新しい入力デバイスの追加
- 大規模な domain object 分割

## Work Items

- 既存 UseCase は維持する。
- `ApplyInteractionObjectsUseCase` の責務を明確化する。
- 入力物体の適用、`InteractionField` 再生成、`WalkableSurface` 再生成の流れを読みやすくする。
- 必要なら `World.SetInteractionObjects` 内の派生生成処理を小さな domain service 的メソッドへ分割する。
- Unity controller が domain ルールを直接知らない状態を維持する。

## Acceptance Check

- Unity 側は `orchestrator.AdvanceFrame(deltaTime, interactionObjects)` を呼ぶだけで済む
- 入力元をマウスから UDP に変えても、小人挙動のコード変更が不要
- `WalkableSurface` 生成ルールを単体で読みやすい
- UseCase の責務が docs とコードで一致している

## Handoff Notes

Milestone 2 の挙動修正が安定してから着手する。先に UseCase を大きく動かすと、挙動調整時の原因切り分けが難しくなる。
