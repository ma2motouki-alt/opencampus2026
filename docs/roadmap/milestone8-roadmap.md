# Milestone 8: RealSense Prototype

## Goal

RealSense D435 と Python 認識アプリから、Unity に物体座標を送れる状態にする。

## Target Scope

- Python vision app
- RealSense D435
- `pyrealsense2`
- OpenCV
- depth threshold
- contour detection
- plane calibration
- UDP JSON send

対象外:

- 高精度な自由物体分類
- 展示本番の固定リグ最適化
- Unity 側 domain logic の変更

## Work Items

- RealSense D435 から depth frame を取得する。
- 平面キャリブレーションでディスプレイ面を推定する。
- 深度しきい値で手やプロップ候補を抽出する。
- 輪郭抽出で位置、サイズ、角度を推定する。
- 専用プロップの丸・棒・ブロック相当を `InteractionObject` JSON に変換する。
- UDP で Unity に送信する。

## Acceptance Check

- RealSense から取得した物体が Unity 上に表示される
- 棒プロップが `bar_prop` として Unity に届く
- 物体を動かすと Unity 側の位置も追従する
- マウス入力と同じ domain logic で小人が反応する

## Handoff Notes

最初は専用プロップ前提でよい。自由分類よりも、安定して normalized coordinate を送れることを優先する。
