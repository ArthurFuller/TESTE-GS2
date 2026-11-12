using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PauseToggle : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private Image resumeArtwork;
    [SerializeField] private CanvasGroup pauseOverlay;
    [SerializeField] private Button missionsButton;

    private bool paused;
    private Canvas buttonCanvas;
    private TopBarAutoHide topBar;

    private void Awake()
    {
        topBar = GetComponentInParent<TopBarAutoHide>();
        if (button != null) buttonCanvas = button.GetComponent<Canvas>();
        if (button != null) button.onClick.AddListener(Toggle);
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Toggle);
        if (paused)
        {
            topBar?.SetPaused(false);
            Time.timeScale = 1f;
        }
    }

    public void Toggle()
    {
        paused = !paused;
        AudioManager.Play(paused ? SoundCue.PanelOpen : SoundCue.PanelClose);
        Time.timeScale = paused ? 0f : 1f;
        topBar?.SetPaused(paused);
        if (paused && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        // Keep only the existing pause/return button above the overlay.
        if (buttonCanvas != null)
        {
            buttonCanvas.overrideSorting = paused;
            buttonCanvas.sortingOrder = paused ? 5 : 0;
        }
        if (label != null) label.text = paused ? "" : "II";
        if (resumeArtwork != null) resumeArtwork.enabled = paused;
        if (missionsButton != null) missionsButton.interactable = !paused;
        if (pauseOverlay == null) return;
        pauseOverlay.alpha = paused ? 1f : 0f;
        pauseOverlay.interactable = paused;
        pauseOverlay.blocksRaycasts = paused;
    }
}
