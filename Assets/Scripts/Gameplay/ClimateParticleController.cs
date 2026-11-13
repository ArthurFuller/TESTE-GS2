using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// VFX apenas: o evento continua sendo a fonte de verdade da gameplay.
public sealed class ClimateParticleController : MonoBehaviour
{
    [System.Serializable]
    private sealed class TumbleweedRoute
    {
        public Vector3 start;
        public Vector3 end;
    }

    [SerializeField] private DayEventController events;
    [SerializeField] private ParticleSystem rain;
    [SerializeField] private ParticleSystem droughtDust;
    [SerializeField] private CanvasGroup droughtTint;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.65f;
    [Header("Bola do deserto")]
    [SerializeField] private GameObject tumbleweedPrefab;
    [SerializeField, Min(0.1f), Tooltip("Diâmetro da bola em unidades da cena.")] private float tumbleweedDiameter = 0.85f;
    [SerializeField, Min(0.1f), Tooltip("Unidades da cena percorridas por segundo.")] private float tumbleweedMoveSpeed = 3.5f;
    [SerializeField, Min(0f), Tooltip("Graus de rotação por segundo.")] private float tumbleweedRotationSpeed = 360f;
    [SerializeField, Min(1f)] private float tumbleweedIntervalMin = 18f;
    [SerializeField, Min(1f)] private float tumbleweedIntervalMax = 32f;
    [SerializeField, Tooltip("Cada caminho tem um ponto inicial e um final em coordenadas da cena.")] private TumbleweedRoute[] tumbleweedRoutes;

    private Tween tintFade;
    private bool raining;
    private bool drought;
    private UniversalAdditionalCameraData cameraData;
    private CameraOverrideOption previousColorOption;
    private GameObject tumbleweed;
    private Tween tumbleweedTravel;
    private float nextTumbleweedTime;

    private void Awake()
    {
#if UNITY_EDITOR
        Transform preview = transform.Find("Prévia - Bola do deserto");
        if (preview != null) preview.gameObject.SetActive(false);
#endif
        if (gameplayCamera != null)
            gameplayCamera.TryGetComponent(out cameraData);
        if (cameraData != null)
            previousColorOption = cameraData.requiresColorOption;
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
        if (drought && tumbleweed == null && Time.time >= nextTumbleweedTime)
        {
            SpawnTumbleweed();
            ScheduleTumbleweed();
        }
    }

    private void OnEnable()
    {
        rain?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        droughtDust?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (droughtTint != null) droughtTint.alpha = 0f;
        RemoveTumbleweed();
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
        RemoveTumbleweed();
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
            if (nextDrought)
            {
                ScheduleTumbleweed();
            }
            else RemoveTumbleweed();
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

    private void ScheduleTumbleweed()
    {
        nextTumbleweedTime = Time.time + Random.Range(tumbleweedIntervalMin,
            Mathf.Max(tumbleweedIntervalMin, tumbleweedIntervalMax));
    }

    private void SpawnTumbleweed()
    {
        if (tumbleweedPrefab == null ||
            tumbleweedRoutes == null || tumbleweedRoutes.Length == 0) return;
        TumbleweedRoute route = tumbleweedRoutes[Random.Range(0, tumbleweedRoutes.Length)];
        if (route == null) return;
        Vector3 start = route.start;
        Vector3 end = route.end;
        if ((end - start).sqrMagnitude < 0.01f) return;

        tumbleweed = Instantiate(tumbleweedPrefab, start, Quaternion.identity);
        tumbleweed.name = "Bola do deserto - seca";
        Renderer[] renderers = tumbleweed.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { RemoveTumbleweed(); return; }
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        tumbleweed.transform.localScale *= tumbleweedDiameter /
            Mathf.Max(0.01f, bounds.size.x, bounds.size.y, bounds.size.z);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float groundOffset = start.y - bounds.min.y;
        tumbleweed.transform.position += Vector3.up * groundOffset;
        end.y += groundOffset;
        foreach (Collider collider in tumbleweed.GetComponentsInChildren<Collider>())
            collider.enabled = false;

        Vector3 rollAxis = Vector3.Cross(Vector3.up, end - tumbleweed.transform.position).normalized;
        float travelSeconds = Vector3.Distance(tumbleweed.transform.position, end) /
            Mathf.Max(0.1f, tumbleweedMoveSpeed);
        Quaternion initialRotation = tumbleweed.transform.rotation;
        tumbleweedTravel = DOTween.Sequence().SetTarget(this)
            .Join(tumbleweed.transform.DOMove(end, travelSeconds).SetEase(Ease.Linear))
            .Join(DOTween.To(() => 0f,
                angle => tumbleweed.transform.rotation = Quaternion.AngleAxis(angle, rollAxis) * initialRotation,
                tumbleweedRotationSpeed * travelSeconds, travelSeconds).SetEase(Ease.Linear))
            .OnComplete(RemoveTumbleweed);
    }

    private void RemoveTumbleweed()
    {
        tumbleweedTravel?.Kill();
        tumbleweedTravel = null;
        if (tumbleweed != null) Destroy(tumbleweed);
        tumbleweed = null;
    }
}
