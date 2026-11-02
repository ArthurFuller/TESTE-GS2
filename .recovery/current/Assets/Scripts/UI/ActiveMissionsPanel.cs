using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public sealed class ActiveMissionsPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private Text toggleText;
    [SerializeField] private ActiveMissionCard[] cards;
    [SerializeField] private Sprite[] itemIcons;
    [SerializeField, Min(0)] private float animationDuration = 0.28f;
    [SerializeField, Min(0)] private float hiddenOffset = 24f;
    private bool isOpen;
    private Vector2 openPosition;

    private void Awake()
    {
        if (contentRect != null) openPosition = contentRect.anchoredPosition;
        ApplyOpenState(true);
    }

    public void Toggle()
    {
        isOpen = !isOpen;
        ApplyOpenState(false);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        ApplyOpenState(false);
    }

    private void ApplyOpenState(bool immediate)
    {
        if (contentGroup != null)
        {
            contentGroup.DOKill();
            if (immediate) contentGroup.alpha = isOpen ? 1 : 0;
            else contentGroup.DOFade(isOpen ? 1 : 0, animationDuration);
            contentGroup.interactable = isOpen;
            contentGroup.blocksRaycasts = isOpen;
        }
        if (contentRect != null)
        {
            contentRect.DOKill();
            if (immediate) contentRect.anchoredPosition = openPosition + Vector2.up * (isOpen ? 0 : hiddenOffset);
            else contentRect.DOAnchorPos(openPosition + Vector2.up * (isOpen ? 0 : hiddenOffset),
                animationDuration).SetEase(Ease.OutCubic);
        }
        if (toggleText != null) toggleText.text = isOpen ? "FECHAR" : "MISSÕES";
    }

    public void Refresh(IReadOnlyList<DeliveryMission> missions, int day, ResourceBank resources)
    {
        if (cards == null) return;
        int shown = 0;
        for (int i = 0; i < missions.Count && shown < cards.Length; i++)
        {
            DeliveryMission mission = missions[i];
            if (!mission.accepted) continue;
            ActiveMissionCard card = cards[shown++];
            if (card == null) continue;
            int stock = mission.item switch
            {
                DeliveryItem.Wood => resources.Wood,
                DeliveryItem.Food => resources.Food,
                DeliveryItem.CornSeeds => resources.CornSeeds,
                DeliveryItem.PumpkinSeeds => resources.PumpkinSeeds,
                DeliveryItem.Fish => resources.Fish,
                DeliveryItem.Corn => resources.Corn,
                _ => resources.Pumpkin
            };
            card.gameObject.SetActive(true);
            Sprite icon = itemIcons != null && (int)mission.item < itemIcons.Length ? itemIcons[(int)mission.item] : null;
            card.Bind(mission, day, stock, icon);
        }
        for (int i = shown; i < cards.Length; i++)
            if (cards[i] != null) cards[i].gameObject.SetActive(false);
    }
}
