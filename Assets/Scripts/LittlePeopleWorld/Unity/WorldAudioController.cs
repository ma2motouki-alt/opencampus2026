using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class WorldAudioController : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] AudioClip baseAmbientClip;
        [SerializeField] AudioClip rainLayerClip;
        [SerializeField] AudioClip plantGrowthLayerClip;
        [SerializeField] AudioClip plantStartClip;
        [SerializeField] AudioClip plantBloomClip;
        [SerializeField] AudioClip flowerBurstClip;
        [SerializeField] AudioClip rainbowClip;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] float baseAmbientVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] float rainLayerVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] float plantGrowthLayerVolume = 0.25f;
        [SerializeField, Range(0f, 1f)] float plantStartVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] float plantBloomVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float flowerBurstVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] float rainbowVolume = 0.65f;
        [SerializeField] float layerFadeSeconds = 1.2f;
        [SerializeField, Min(0f)] float rainLayerHoldSeconds = 1.5f;

        [Header("Layer Switches")]
        [SerializeField] bool playBaseAmbient = true;
        [SerializeField] bool enableRainLayer = true;
        [SerializeField] bool enablePlantGrowthLayer = true;
        [SerializeField] bool enablePlantStartOneShot = true;
        [SerializeField] bool enablePlantBloomOneShot = true;
        [SerializeField] bool enableFlowerBurstOneShot = true;
        [SerializeField] bool enableRainbowOneShot = true;

        AudioSource baseAmbientSource;
        AudioSource rainLayerSource;
        AudioSource plantGrowthLayerSource;
        AudioSource oneShotSource;
        int lastPlantSpawnSequence;
        int lastPlantBloomSequence;
        int lastFlowerBurstSequence;
        int lastRainbowSpawnSequence;
        bool hasPlantSpawnSequence;
        bool hasPlantBloomSequence;
        bool hasFlowerBurstSequence;
        bool hasRainbowSpawnSequence;
        float rainLayerHoldTimer;

        public void UpdateAudio(
            World world,
            MasterDatabase masters,
            float deltaTime,
            bool plantGrowthActive,
            int plantSpawnSequence,
            int plantBloomSequence,
            int flowerBurstSequence,
            int rainbowSpawnSequence)
        {
            EnsureSources();

            var hasRain = HasRainColumn(world, masters);
            var keepRainLayerPlaying = UpdateRainLayerActivity(hasRain, deltaTime);
            var volumeMultiplier = Mathf.Clamp01(masterVolume);
            UpdateLoopSource(
                baseAmbientSource,
                baseAmbientClip,
                playBaseAmbient,
                volumeMultiplier * baseAmbientVolume,
                deltaTime);
            UpdateLoopSource(
                rainLayerSource,
                rainLayerClip,
                enableRainLayer && keepRainLayerPlaying,
                volumeMultiplier * rainLayerVolume,
                deltaTime);
            UpdateLoopSource(
                plantGrowthLayerSource,
                plantGrowthLayerClip,
                enablePlantGrowthLayer && plantGrowthActive,
                volumeMultiplier * plantGrowthLayerVolume,
                deltaTime);
            UpdateOneShotSequence(
                ref lastPlantSpawnSequence,
                ref hasPlantSpawnSequence,
                plantSpawnSequence,
                enablePlantStartOneShot,
                plantStartClip,
                volumeMultiplier * plantStartVolume);
            UpdateOneShotSequence(
                ref lastPlantBloomSequence,
                ref hasPlantBloomSequence,
                plantBloomSequence,
                enablePlantBloomOneShot,
                plantBloomClip,
                volumeMultiplier * plantBloomVolume);
            UpdateOneShotSequence(
                ref lastFlowerBurstSequence,
                ref hasFlowerBurstSequence,
                flowerBurstSequence,
                enableFlowerBurstOneShot,
                flowerBurstClip,
                volumeMultiplier * flowerBurstVolume);
            UpdateOneShotSequence(
                ref lastRainbowSpawnSequence,
                ref hasRainbowSpawnSequence,
                rainbowSpawnSequence,
                enableRainbowOneShot,
                rainbowClip,
                volumeMultiplier * rainbowVolume);
        }

        public void SetAudioActive(bool active)
        {
            EnsureSources();
            if (active)
            {
                return;
            }

            StopSource(baseAmbientSource);
            StopSource(rainLayerSource);
            StopSource(plantGrowthLayerSource);
            StopSource(oneShotSource);
            hasPlantSpawnSequence = false;
            hasPlantBloomSequence = false;
            hasFlowerBurstSequence = false;
            hasRainbowSpawnSequence = false;
            rainLayerHoldTimer = 0f;
        }

        void EnsureSources()
        {
            baseAmbientSource ??= CreateLoopSource("Base Ambient Audio");
            rainLayerSource ??= CreateLoopSource("Rain Layer Audio");
            plantGrowthLayerSource ??= CreateLoopSource("Plant Growth Layer Audio");
            oneShotSource ??= CreateOneShotSource("World One Shot Audio");
        }

        AudioSource CreateLoopSource(string sourceName)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        AudioSource CreateOneShotSource(string sourceName)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            return source;
        }

        void UpdateLoopSource(
            AudioSource source,
            AudioClip clip,
            bool shouldPlay,
            float targetVolume,
            float deltaTime)
        {
            if (source == null)
            {
                return;
            }

            if (source.clip != clip)
            {
                source.Stop();
                source.clip = clip;
                source.volume = 0f;
            }

            if (clip == null)
            {
                source.volume = 0f;
                return;
            }

            targetVolume = shouldPlay ? Mathf.Clamp01(targetVolume) : 0f;
            if (targetVolume > 0.001f && !source.isPlaying)
            {
                source.Play();
            }

            source.volume = FadeVolume(source.volume, targetVolume, deltaTime);
            if (targetVolume <= 0.001f && source.volume <= 0.001f && source.isPlaying)
            {
                source.Stop();
                source.volume = 0f;
            }
        }

        bool UpdateRainLayerActivity(bool hasRain, float deltaTime)
        {
            if (hasRain)
            {
                rainLayerHoldTimer = Mathf.Max(0f, rainLayerHoldSeconds);
                return true;
            }

            rainLayerHoldTimer = Mathf.Max(0f, rainLayerHoldTimer - Mathf.Max(0f, deltaTime));
            return rainLayerHoldTimer > 0f;
        }

        void UpdateOneShotSequence(
            ref int lastSequence,
            ref bool hasSequence,
            int sequence,
            bool enabled,
            AudioClip clip,
            float targetVolume)
        {
            if (!hasSequence)
            {
                lastSequence = sequence;
                hasSequence = true;
                return;
            }

            if (sequence <= lastSequence)
            {
                lastSequence = sequence;
                return;
            }

            var playCount = Mathf.Min(sequence - lastSequence, 3);
            lastSequence = sequence;

            if (!enabled || clip == null || oneShotSource == null)
            {
                return;
            }

            var volume = Mathf.Clamp01(targetVolume);
            for (var i = 0; i < playCount; i++)
            {
                oneShotSource.PlayOneShot(clip, volume);
            }
        }

        float FadeVolume(float current, float target, float deltaTime)
        {
            if (layerFadeSeconds <= 0.001f)
            {
                return target;
            }

            var step = Mathf.Max(0f, deltaTime) / layerFadeSeconds;
            return Mathf.MoveTowards(current, target, step);
        }

        static bool HasRainColumn(World world, MasterDatabase masters)
        {
            if (world == null || masters == null)
            {
                return false;
            }

            foreach (var visualEffect in world.VisualEffects)
            {
                var effectMaster = masters.VisualEffects.Get(visualEffect.VisualEffectMasterId);
                if (effectMaster.Kind == VisualEffectKind.RainColumn)
                {
                    return true;
                }
            }

            return false;
        }

        static void StopSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.volume = 0f;
        }
    }
}