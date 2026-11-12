using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// VFX apenas: o evento continua sendo a fonte de verdade da gameplay.
public sealed class ClimateParticleController : MonoBehaviour
{
    [SerializeField] private DayEventController events;
    [SerializeField] private ParticleSystem rain;
    [SerializeField] private ParticleSystem rainSplashes;
    [SerializeField] private ParticleSystem droughtDust;
    [SerializeField] private CanvasGroup droughtTint;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform groundHeightReference;
    [SerializeField, Min(0f)] private float dustSpawnHeight = 0.35f;
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.65f;

    private Tween tintFade;
    private bool raining;
    private bool drought;
    private UniversalAdditionalCameraData cameraData;
    private CameraOverrideOption previousColorOption;

    private void Awake()
    {
        if (gameplayCamera != null)
            gameplayCamera.TryGetComponent(out cameraData);
        if (cameraData != null)
            previousColorOption = cameraData.requiresColorOption;

        if (rain != null)
        {
            var main = rain.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var collision = rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.dampen = 1f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;

            if (rainSplashes != null)
            {
                var splashesMain = rainSplashes.main;
                splashesMain.simulationSpace = ParticleSystemSimulationSpace.World;
                splashesMain.loop = false;
                var splashEmission = rainSplashes.emission;
                splashEmission.rateOverTime = 0f;
                splashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1, 1) });
                var splashSize = rainSplashes.sizeOverLifetime;
                splashSize.enabled = true;
                splashSize.size = new ParticleSystem.MinMaxCurve(1f,
                    new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.5f)));
                var splashColor = rainSplashes.colorOverLifetime;
                splashColor.enabled = true;
                var splashGradient = new Gradient();
                splashGradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
                splashColor.color = new ParticleSystem.MinMaxGradient(splashGradient);
                var subEmitters = rain.subEmitters;
                subEmitters.enabled = true;
                for (int i = subEmitters.subEmittersCount - 1; i >= 0; i--)
                    if (subEmitters.GetSubEmitterSystem(i) == null)
                        subEmitters.RemoveSubEmitter(i);
                bool hasSplashes = false;
                for (int i = 0; i < subEmitters.subEmittersCount; i++)
                    if (subEmitters.GetSubEmitterSystem(i) == rainSplashes)
                    {
                        subEmitters.SetSubEmitterType(i, ParticleSystemSubEmitterType.Collision);
                        subEmitters.SetSubEmitterProperties(i,
                            ParticleSystemSubEmitterProperties.InheritNothing);
                        subEmitters.SetSubEmitterEmitProbability(i, 1f);
                        hasSplashes = true;
                    }
                if (!hasSplashes)
                    subEmitters.AddSubEmitter(rainSplashes, ParticleSystemSubEmitterType.Collision,
                        ParticleSystemSubEmitterProperties.InheritNothing, 1f);
            }
        }
        if (droughtDust != null)
        {
            var smokeMain = droughtDust.main;
            smokeMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var smokeSize = droughtDust.sizeOverLifetime;
            smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.8f)));
            var smokeColor = droughtDust.colorOverLifetime;
            smokeColor.enabled = true;
            var smokeGradient = new Gradient();
            smokeGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.25f),
                    new GradientAlphaKey(0f, 1f) });
            smokeColor.color = new ParticleSystem.MinMaxGradient(smokeGradient);
        }
        UpdateEmitterPositions();
    }

    private void LateUpdate()
    {
        UpdateEmitterPositions();
    }

    private void Update()
    {
        if (events == null) return;
        if (events.IsRaining != raining || events.IsDrought != drought)
            Refresh();
        if (raining && rain != null && !rain.isPlaying)
            rain.Play(true);
        if (drought && droughtDust != null && !droughtDust.isPlaying)
            droughtDust.Play(true);
    }

    private void UpdateEmitterPositions()
    {
        if (gameplayCamera == null || groundHeightReference == null) return;
        var ray = gameplayCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (droughtDust != null)
        {
            var ground = new Plane(Vector3.up, groundHeightReference.position);
            if (ground.Raycast(ray, out float groundDistance))
                droughtDust.transform.SetPositionAndRotation(
                    ray.GetPoint(groundDistance) + Vector3.up * dustSpawnHeight,
                    Quaternion.identity);
        }
    }

    private void OnEnable()
    {
        rain?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        droughtDust?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (droughtTint != null) droughtTint.alpha = 0f;
        raining = drought = false;
        if (events != null) events.WeatherChanged += Refresh;
        Refresh();
    }
    private void OnDisable()
    {
        if (events != null) events.WeatherChanged -= Refresh;
        tintFade?.Kill();
        rain?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        droughtDust?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (droughtTint != null) droughtTint.alpha = 0f;
        if (cameraData != null) cameraData.requiresColorOption = previousColorOption;
    }

    private void Refresh()
    {
        bool nextRain = events != null && events.IsRaining;
        bool nextDrought = events != null && events.IsDrought;
        if (nextRain != raining)
        {
            raining = nextRain;
            ApplyEmission(rain, nextRain);
        }
        if (nextDrought != drought)
        {
            drought = nextDrought;
            if (cameraData != null)
                cameraData.requiresColorOption = nextDrought
                    ? CameraOverrideOption.On : previousColorOption;
            ApplyEmission(droughtDust, nextDrought);
            if (droughtTint != null)
            {
                tintFade?.Kill();
                tintFade = droughtTint.DOFade(nextDrought ? 1f : 0f, transitionSeconds)
                    .SetEase(Ease.InOutSine).SetUpdate(true);
            }
        }
    }

    private static void ApplyEmission(ParticleSystem system, bool active)
    {
        if (system == null) return;
        if (active) system.Play(true);
        else system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
