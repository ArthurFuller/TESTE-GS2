using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

// Canvas da cena inicial que cobre a troca de cena e clareia a gameplay depois do carregamento.
public sealed class GS2SceneFade : MonoBehaviour
{
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField, Min(0.1f)] private float fadeSeconds = 0.42f;

    private Tween fade;
    private string destination;
    private bool loading;

    private void Awake()
    {
        if (blackOverlay == null) blackOverlay = GetComponent<CanvasGroup>();
        if (blackOverlay == null) return;
        blackOverlay.alpha = 1f;
        blackOverlay.interactable = false;
        blackOverlay.blocksRaycasts = true;
        fade = blackOverlay.DOFade(0f, fadeSeconds).SetUpdate(true)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => blackOverlay.blocksRaycasts = false);
    }

    public void EnterGameplay(string sceneName)
    {
        if (loading || blackOverlay == null || string.IsNullOrEmpty(sceneName)) return;
        loading = true;
        destination = sceneName;
        blackOverlay.blocksRaycasts = true;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        fade?.Kill();
        fade = blackOverlay.DOFade(1f, fadeSeconds).SetUpdate(true)
            .SetEase(Ease.InOutSine).OnComplete(() => SceneManager.LoadScene(destination));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!loading || scene.name != destination) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        fade?.Kill();
        fade = blackOverlay.DOFade(0f, fadeSeconds).SetUpdate(true)
            .SetEase(Ease.InOutSine).OnComplete(() => Destroy(gameObject));
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        fade?.Kill();
    }
}
