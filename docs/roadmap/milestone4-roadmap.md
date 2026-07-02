# Milestone 4: Ambient Reaction Completion

## Goal

雲と星が小人ワールド内の自然な環境オブジェクトとして機能し、展示の発見要素になる。

## Target Scope

- `AmbientObject`
- `VisualEffectInstance`
- `AmbientObjectView`
- `VisualEffectView`
- `AmbientObjectTypeMaster`
- `VisualEffectMaster`

対象外:

- 泡などの追加環境オブジェクト
- 本番 prefab 素材
- 音演出の作り込み

## Work Items

- Cloud touch -> rain column の体験を調整する。
- Star touch -> star burst の体験を調整する。
- Cooldown / linger の値を `TuningParameterMaster` で調整できる状態にする。
- Falling 中の小人は ambient reaction を起こさないルールを維持する。
- `VisualEffectMaster.RenderMode` / `AssetKey` を後の素材差し替え境界として維持する。

## Acceptance Check

- 雲に小人が触れると雨が降る
- 雲から離れると雨が自然に止まる
- 星に小人が触れると光が弾ける
- 星は連続発火せず、クールダウン後に再反応する
- 棒歩行や落下と干渉しない

## Handoff Notes

この milestone では新しい種類の環境オブジェクトは増やさない。既存の雲・星を展示として気持ちよくすることに集中する。
