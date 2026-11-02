using UnityEngine;
using UnityEngine.UI;

public sealed class InitialTutorial : MonoBehaviour
{
    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private Text description;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button[] disabledDuringTutorial;
    [SerializeField, TextArea] private string firstDescription =
        "Aqui estão os recursos, o dia e seu dinheiro. Use os botões à direita para ver as missões ou pausar.";

    private void Awake()
    {
        if (description != null) description.text = firstDescription;
        if (continueButton != null) continueButton.onClick.AddListener(Close);
        SetVisible(true);
    }

    private void Start()
    {
        foreach (Button button in disabledDuringTutorial)
            if (button != null) button.interactable = false;
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Close);
    }

    public void Close()
    {
        SetVisible(false);
        foreach (Button button in disabledDuringTutorial)
            if (button != null) button.interactable = true;
    }

    private void SetVisible(bool visible)
    {
        if (overlay == null) return;
        overlay.alpha = visible ? 1f : 0f;
        overlay.interactable = visible;
        overlay.blocksRaycasts = visible;
    }
}
