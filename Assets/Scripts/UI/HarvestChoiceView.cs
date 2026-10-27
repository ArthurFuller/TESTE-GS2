using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class HarvestChoiceView : MonoBehaviour
{
    [SerializeField] private ActionResolver actions;
    [SerializeField] private CanvasGroup choiceGroup;
    [SerializeField] private Text titleText;
    [SerializeField] private Text foodButtonText;
    [SerializeField] private Text scoreButtonText;
    [SerializeField, Min(0)] private float fadeDuration = 0.25f;

    private void Awake()
    {
        Hide();
    }

    public void Show(CropSettings crop, int foodYield)
    {
        if (choiceGroup == null || crop == null)
            return;
        if (titleText != null) titleText.text = "Colheita de " + crop.displayName;
        if (foodButtonText != null) foodButtonText.text = "+" + foodYield + " COMIDAS";
        if (scoreButtonText != null) scoreButtonText.text = "+" + crop.scoreYield + " PONTOS";
        choiceGroup.DOKill();
        choiceGroup.blocksRaycasts = true;
        choiceGroup.interactable = true;
        choiceGroup.DOFade(1, fadeDuration);
    }

    public void ChooseFood()
    {
        if (actions != null) actions.CompleteHarvest(true);
        Hide();
    }

    public void ChooseScore()
    {
        if (actions != null) actions.CompleteHarvest(false);
        Hide();
    }

    private void Hide()
    {
        if (choiceGroup == null)
            return;
        choiceGroup.DOKill();
        choiceGroup.blocksRaycasts = false;
        choiceGroup.interactable = false;
        choiceGroup.DOFade(0, fadeDuration);
    }
}
