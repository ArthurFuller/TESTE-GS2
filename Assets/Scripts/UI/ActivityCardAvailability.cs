using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ActivityCardAvailability : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter
{
    [SerializeField] private ActivityCardDrag[] cards;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private SeedMerchant merchant;
    [Header("Disposição da mão")]
    [SerializeField, Range(5, 15)] private int maxHandCards = 15;
    [SerializeField, Min(140f)] private float handWidth = 480f;
    [SerializeField, Min(1f)] private float cardSpacing = 110f;
    [SerializeField] private float cardBaseY = 16f;
    [SerializeField, Min(0f)] private float arcHeight = 10f;
    [SerializeField, Range(0f, 30f)] private float arcAngle = 12f;
    [Header("Carta selecionada")]
    [SerializeField, Min(0f)] private float hoverLift = 18f;
    [SerializeField, Min(1f)] private float hoverScale = 1.08f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.16f;
    [SerializeField, Min(0f)] private float hoverSpread = 22f;
    [SerializeField] private RectTransform descriptionPanel;
    [SerializeField] private Text descriptionText;
    [SerializeField, Min(0f)] private float descriptionGap = 12f;

    private readonly List<ActivityCardDrag> shown = new List<ActivityCardDrag>();
    private ActivityCardDrag hovered;
    private ActivityCardDrag dragging;
    private bool pointerInside;
    private float firstCenter;
    private float spacing;
    private float maxCardHeight;
    private int nextCardId;

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += ApplyInspectorChanges;
            return;
        }
#endif
        if (resources != null) resources.Changed += Refresh;
        if (merchant != null) merchant.AvailabilityChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= ApplyInspectorChanges;
#endif
        if (!Application.isPlaying) return;
        if (resources != null) resources.Changed -= Refresh;
        if (merchant != null) merchant.AvailabilityChanged -= Refresh;
        ClearHover(true);
        if (dragging != null) dragging.CancelDrag();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyInspectorChanges;
        UnityEditor.EditorApplication.delayCall += ApplyInspectorChanges;
    }

    private void ApplyInspectorChanges()
    {
        if (this == null || cards == null || dragging != null) return;
        ActivityCardDrag selected = hovered;
        if (Application.isPlaying) ClearHover(true);
        if (Application.isPlaying) Refresh();
        else CollectEditorCards();
        Layout(true);
        if (Application.isPlaying && selected != null && shown.Contains(selected)) SetHover(selected);
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying || !pointerInside || dragging != null || Mouse.current == null) return;
        SetHover(CardAt(Mouse.current.position.ReadValue(), null));
    }

    public void Refresh()
    {
        if (!Application.isPlaying || cards == null || resources == null) return;
        bool changed = false;
        if (shown.Count == 0)
        {
            foreach (ActivityCardDrag card in cards)
            {
                if (card == null || IsSeed(card.Kind) || card.Kind == ActivityKind.BuySeeds) continue;
                SetVisible(card, true);
                shown.Add(card);
            }
            foreach (ActivityCardDrag card in cards)
                if (card != null && IsSeed(card.Kind)) SetVisible(card, false);
            changed = true;
        }

        foreach (ActivityCardDrag card in cards)
            if (card != null && card.Kind == ActivityKind.BuySeeds)
                SetVisible(card, merchant != null && merchant.IsAvailable);

        if (CountCards(ActivityKind.PlantCorn) > resources.CornSeeds ||
            CountCards(ActivityKind.PlantPumpkin) > resources.PumpkinSeeds || shown.Count > maxHandCards)
        {
            ClearHover(true);
            while (CountCards(ActivityKind.PlantCorn) > resources.CornSeeds) RemoveLast(ActivityKind.PlantCorn);
            while (CountCards(ActivityKind.PlantPumpkin) > resources.PumpkinSeeds) RemoveLast(ActivityKind.PlantPumpkin);
            while (shown.Count > maxHandCards) RemoveLastSeed();
            changed = true;
        }

        if (shown.Count < maxHandCards &&
            (CountCards(ActivityKind.PlantCorn) < resources.CornSeeds ||
             CountCards(ActivityKind.PlantPumpkin) < resources.PumpkinSeeds))
            ClearHover(true);

        while (shown.Count < maxHandCards && CountCards(ActivityKind.PlantCorn) < resources.CornSeeds)
        {
            if (!AddCard(ActivityKind.PlantCorn)) break;
            changed = true;
        }
        while (shown.Count < maxHandCards && CountCards(ActivityKind.PlantPumpkin) < resources.PumpkinSeeds)
        {
            if (!AddCard(ActivityKind.PlantPumpkin)) break;
            changed = true;
        }
        if (changed) Layout(true);
    }

    private void CollectEditorCards()
    {
        shown.Clear();
        foreach (ActivityCardDrag card in cards)
            if (card != null && card.gameObject.activeSelf && card.Kind != ActivityKind.BuySeeds)
                shown.Add(card);
    }

    private static bool IsSeed(ActivityKind kind) =>
        kind == ActivityKind.PlantCorn || kind == ActivityKind.PlantPumpkin;

    private int CountCards(ActivityKind kind)
    {
        int count = 0;
        foreach (ActivityCardDrag card in shown)
            if (card.Kind == kind) count++;
        return count;
    }

    private bool AddCard(ActivityKind kind)
    {
        foreach (ActivityCardDrag template in cards)
        {
            if (template == null || template.Kind != kind) continue;
            GameObject copy = Instantiate(template.gameObject, transform.parent);
            copy.name = template.gameObject.name + " " + ++nextCardId;
            copy.SetActive(true);
            shown.Add(copy.GetComponent<ActivityCardDrag>());
            return true;
        }
        return false;
    }

    private void RemoveLast(ActivityKind kind)
    {
        for (int i = shown.Count - 1; i >= 0; i--)
        {
            if (shown[i].Kind != kind) continue;
            GameObject removed = shown[i].gameObject;
            shown.RemoveAt(i);
            removed.SetActive(false);
            Destroy(removed);
            return;
        }
    }

    private void RemoveLastSeed()
    {
        for (int i = shown.Count - 1; i >= 0; i--)
            if (IsSeed(shown[i].Kind))
            {
                RemoveLast(shown[i].Kind);
                return;
            }
    }

    private static void SetVisible(ActivityCardDrag card, bool visible)
    {
        if (card.gameObject.activeSelf != visible) card.gameObject.SetActive(visible);
    }

    private void Layout(bool orderCards)
    {
        if (shown.Count == 0) return;
        RectTransform area = (RectTransform)transform;
        float width = Mathf.Min(handWidth, area.rect.width);
        float cardWidth = 0f;
        maxCardHeight = 0f;
        foreach (ActivityCardDrag card in shown)
        {
            Rect rect = card.GetComponent<RectTransform>().rect;
            cardWidth = Mathf.Max(cardWidth, rect.width);
            maxCardHeight = Mathf.Max(maxCardHeight, rect.height);
        }
        spacing = shown.Count == 1 ? 0f : Mathf.Max(1f, Mathf.Min(cardSpacing, (width - cardWidth) / (shown.Count - 1)));
        firstCenter = -spacing * (shown.Count - 1) * 0.5f;
        for (int i = 0; i < shown.Count; i++)
        {
            RectTransform rect = shown[i].GetComponent<RectTransform>();
            rect.DOKill();
            float t = shown.Count == 1 ? 0f : i * 2f / (shown.Count - 1) - 1f;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = CardPosition(i);
            rect.localRotation = Quaternion.Euler(0f, 0f, CardAngle(t));
            rect.localScale = Vector3.one;
        }
        if (orderCards) OrderCards();
    }

    private Vector2 CardPosition(int index)
    {
        float t = shown.Count == 1 ? 0f : index * 2f / (shown.Count - 1) - 1f;
        float heightOffset = maxCardHeight - shown[index].GetComponent<RectTransform>().rect.height;
        float drop = FanDrop();
        return new Vector2(firstCenter + index * spacing,
            cardBaseY + heightOffset + drop - arcHeight - drop * t * t);
    }

    private float FanDrop() => arcHeight * Mathf.Min(3.5f, Mathf.Max(1f, (shown.Count - 1) * 0.5f));

    private float CardAngle(float t) => -arcAngle * Mathf.Min(1.8f, Mathf.Max(1f, (shown.Count - 1) * 0.5f)) * t;

    private void OrderCards()
    {
        foreach (ActivityCardDrag card in shown) card.transform.SetAsLastSibling();
        if (descriptionPanel != null && descriptionPanel.gameObject.activeSelf) descriptionPanel.SetAsLastSibling();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (shown.Count == 0) return false;
        for (int i = 0; i < shown.Count; i++)
            if (ContainsCard(i, screenPoint, eventCamera)) return true;
        return false;
    }

    private ActivityCardDrag CardAt(Vector2 screenPoint, Camera eventCamera)
    {
        RectTransform area = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, eventCamera, out Vector2 point))
            return null;
        float x = point.x - area.rect.center.x;
        ActivityCardDrag result = null;
        float nearest = float.MaxValue;
        for (int i = 0; i < shown.Count; i++)
        {
            if (!ContainsCard(i, screenPoint, eventCamera)) continue;
            float distance = Mathf.Abs(x - CardPosition(i).x);
            if (distance >= nearest) continue;
            nearest = distance;
            result = shown[i];
        }
        return result;
    }

    private bool ContainsCard(int index, Vector2 screenPoint, Camera eventCamera)
    {
        RectTransform rect = shown[index].GetComponent<RectTransform>();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera,
                out Vector2 current)) return false;
        Rect visible = VisibleRect(rect, shown[index].GetComponent<Image>());
        if (visible.Contains(current)) return true;

        RectTransform parent = (RectTransform)rect.parent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, eventCamera,
                out Vector2 point)) return false;
        float t = shown.Count == 1 ? 0f : index * 2f / (shown.Count - 1) - 1f;
        Vector2 baseline = Quaternion.Euler(0f, 0f, -CardAngle(t)) * (point - CardPosition(index));
        return visible.Contains(baseline);
    }

    private static Rect VisibleRect(RectTransform rect, Image image)
    {
        Rect bounds = rect.rect;
        if (image == null || !image.preserveAspect || image.sprite == null) return bounds;
        Vector2 sprite = image.sprite.rect.size;
        float scale = Mathf.Min(bounds.width / sprite.x, bounds.height / sprite.y);
        Vector2 size = sprite * scale;
        return new Rect(bounds.center - size * 0.5f, size);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        SetHover(CardAt(eventData.position, eventData.enterEventCamera));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (dragging == null) ClearHover(false);
    }

    private void SetHover(ActivityCardDrag card)
    {
        if (card == hovered) return;
        ClearHover(false);
        hovered = card;
        if (hovered == null) return;
        RectTransform rect = hovered.GetComponent<RectTransform>();
        rect.SetAsLastSibling();
        int index = shown.IndexOf(hovered);
        MoveNeighbours(index, true);
        float top = cardBaseY + maxCardHeight + FanDrop() - arcHeight + hoverLift;
        AnimateCard(rect, new Vector2(CardPosition(index).x, top - rect.rect.height * hoverScale),
            hoverScale, 0f, false);
        if (descriptionPanel == null || descriptionText == null) return;
        descriptionText.text = hovered.Description;
        descriptionPanel.DOKill();
        CanvasGroup group = descriptionPanel.GetComponent<CanvasGroup>();
        if (group != null) group.DOKill();
        Vector2 target = new Vector2(CardPosition(index).x, top + descriptionGap);
        descriptionPanel.anchoredPosition = target - Vector2.up * 8f;
        descriptionPanel.gameObject.SetActive(!string.IsNullOrWhiteSpace(hovered.Description));
        descriptionPanel.SetAsLastSibling();
        if (descriptionPanel.gameObject.activeSelf)
        {
            descriptionPanel.DOAnchorPos(target, hoverDuration).SetEase(Ease.OutCubic);
            if (group != null)
            {
                group.alpha = 0f;
                group.DOFade(1f, hoverDuration);
            }
        }
    }

    private void ClearHover(bool immediate)
    {
        if (hovered == null) return;
        RectTransform rect = hovered.GetComponent<RectTransform>();
        if (descriptionPanel != null)
        {
            descriptionPanel.DOKill();
            CanvasGroup group = descriptionPanel.GetComponent<CanvasGroup>();
            if (group != null) group.DOKill();
            descriptionPanel.gameObject.SetActive(false);
        }
        int index = shown.IndexOf(hovered);
        if (index >= 0)
        {
            MoveNeighbours(index, false, immediate);
            float t = shown.Count == 1 ? 0f : index * 2f / (shown.Count - 1) - 1f;
            AnimateCard(rect, CardPosition(index), 1f, CardAngle(t), immediate);
        }
        hovered = null;
        OrderCards();
    }

    private void MoveNeighbours(int selected, bool apart, bool immediate = false)
    {
        for (int i = 0; i < shown.Count; i++)
        {
            if (i == selected) continue;
            RectTransform rect = shown[i].GetComponent<RectTransform>();
            float offset = apart ? Mathf.Sign(i - selected) * hoverSpread : 0f;
            Vector2 position = CardPosition(i) + Vector2.right * offset;
            float t = shown.Count == 1 ? 0f : i * 2f / (shown.Count - 1) - 1f;
            AnimateCard(rect, position, 1f, CardAngle(t), immediate);
        }
    }

    private void AnimateCard(RectTransform rect, Vector2 position, float scale, float angle, bool immediate)
    {
        rect.DOKill();
        if (immediate)
        {
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * scale;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            return;
        }
        rect.DOAnchorPos(position, hoverDuration).SetEase(Ease.OutCubic);
        rect.DOScale(scale, hoverDuration).SetEase(Ease.OutCubic);
        rect.DOLocalRotate(new Vector3(0f, 0f, angle), hoverDuration).SetEase(Ease.OutCubic);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ActivityCardDrag card = CardAt(eventData.pressPosition, eventData.pressEventCamera);
        if (card == null) return;
        ClearHover(true);
        dragging = card;
        dragging.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging != null) dragging.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragging == null) return;
        dragging.OnEndDrag(eventData);
        dragging = null;
        if (pointerInside) SetHover(CardAt(eventData.position, eventData.enterEventCamera));
    }

}
