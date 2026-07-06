# Milestone 1: Roadmap / Design Sync

## Goal

今後の作業単位を明確にし、design docs、master data、domain object、UseCase の責務を揃える。

## Target Scope

- `docs/roadmap` を milestone ごとの作業計画置き場にする
- `docs/design` を安定した仕様置き場として整理する
- `experience.md` に現在の完成イメージを反映する
- `domain-objects.md` に `WalkableSurface` の端歩きモデルを反映する
- `master-data.md` に surface 調整値を反映する
- `system-design.md` に UseCase 境界と将来入力差し替え方針を反映する

## Work Items

- `docs/roadmap/README.md` を入口として維持する。
- 各 milestone ファイルには `Goal`, `Target Scope`, `Work Items`, `Acceptance Check`, `Handoff Notes` を置く。
- `docs/design/roadmap.md` は `docs/roadmap` への案内に留める。
- 設計仕様と作業計画が食い違った場合は、まず design docs を更新し、その後 roadmap を更新する。

## Acceptance Check

- 次に何を実装すべきかが milestone 単位で判断できる
- domain object と master data の違いが docs 上で崩れていない
- UseCase がどこで使われるか説明できる
- `docs/design` と `docs/roadmap` の役割が重複していない

## Handoff Notes

この milestone は文書構造の基盤作りである。完了後は Milestone 2 の棒挙動修正へ進む。
