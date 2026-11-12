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

    private bool[] previousButtonStates;
    private bool visible;
    public const string SkipKey = "GS2.SkipTutorial";
    public static bool SkipRequested
    {
        get => PlayerPrefs.GetInt(SkipKey, 0) != 0;
        set { PlayerPrefs.SetInt(SkipKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    private void Awake()
    {
        previousButtonStates = new bool[disabledDuringTutorial.Length];
        if (continueButton != null) continueButton.onClick.AddListener(Close);
        if (!SkipRequested) Show(firstDescription);
        else SetVisible(false);
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Close);
    }

    public void Skip()
    {
        SkipRequested = true;
        Close();
    }

    public void Close()
    {
        if (!visible) return;
        visible = false;
        SetVisible(false);
        for (int i = 0; i < disabledDuringTutorial.Length; i++)
            if (disabledDuringTutorial[i] != null)
                disabledDuringTutorial[i].interactable = previousButtonStates[i];
    }

    public void Show(string text)
    {
        if (SkipRequested) return;
        if (description != null) description.text = text;
        if (!visible)
            for (int i = 0; i < disabledDuringTutorial.Length; i++)
                if (disabledDuringTutorial[i] != null)
                {
                    previousButtonStates[i] = disabledDuringTutorial[i].interactable;
                    disabledDuringTutorial[i].interactable = false;
                }
        visible = true;
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (overlay == null) return;
        overlay.alpha = visible ? 1f : 0f;
        overlay.interactable = visible;
        overlay.blocksRaycasts = visible;
    }
}
