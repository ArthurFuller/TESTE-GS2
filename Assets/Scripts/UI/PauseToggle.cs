using UnityEngine;
using UnityEngine.UI;

public sealed class PauseToggle : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private CanvasGroup pauseOverlay;
    [SerializeField] private Button missionsButton;

    private bool paused;

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(Toggle);
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Toggle);
        if (paused) Time.timeScale = 1f;
    }

    public void Toggle()
    {
        paused = !paused;
        Time.timeScale = paused ? 0f : 1f;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (label != null) label.text = paused ? ">" : "II";
        if (missionsButton != null) missionsButton.interactable = !paused;
        if (pauseOverlay == null) return;
        pauseOverlay.alpha = paused ? 1f : 0f;
        pauseOverlay.interactable = paused;
        pauseOverlay.blocksRaycasts = paused;
    }
}
