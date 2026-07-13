using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation.Fairy
{
    internal sealed class FairySettings
    {
        public int Count;
        public float Size;
        public float SpeedPxPerSec;
        public float SteerLerp;
        public Color[] Palette;
        public bool ShowWings;
        public float WingSideOffsetRatio;
        public float WingBackOffsetRatio;
        public float WingWidthRatio;
        public float WingLengthRatio;
        public float WingAlpha;
        public float WingFlapSpeed;
        public float SeparationRadiusPx;
        public float SeparationGain;
        public float TargetJitterPx;
        public float ContourLevel;
        public float CorrectionGain;
        public float SenseThreshold;
        public float PenSenseRadiusPx;
        public float MinPenAttractRadiusPx;
        public float PenAttractRadiusPerSqrtPixel;
        public float EdgeMarginPx;
        public float EdgePullGain;
        public bool EnableCloudRain;
        public float CloudTouchRadius;
        public float CloudRainCooldownSeconds;
        public float BurstInitialSpeedPxPerSec;
        public float BurstFreeSeconds;
        public float BurstReattachCooldownSeconds;
    }
}
