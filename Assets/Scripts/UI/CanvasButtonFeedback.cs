using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Covers scene buttons and buttons created later, such as the timeline's day buttons.
public sealed class CanvasButtonFeedback : MonoBehaviour
{
    [SerializeField, Min(1f)] private float hoverScale = 1.06f;
    [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.93f;
    [SerializeField, Min(0.01f)] private float hoverDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float punchDuration = 0.2f;
    [SerializeField] private Button[] excludedButtons;

    private sealed class ButtonState
    {
        public RectTransform rect;
        public Vector3 baseScale;
        public float factor = 1f;
        public Tween tween;
    }

    private readonly Dictionary<Button, ButtonState> states = new Dictionary<Button, ButtonState>();
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private PointerEventData pointer;
    private EventSystem pointerSystem;
    private Button hovered;

    private void Update()
    {
        if (Mouse.current == null || EventSystem.current == null)
        {
            SetHovered(null);
            return;
        }

        if (pointerSystem != EventSystem.current)
        {
            pointerSystem = EventSystem.current;
            pointer = new PointerEventData(pointerSystem);
        }

        pointer.Reset();
        pointer.position = Mouse.current.position.ReadValue();
        hits.Clear();
        pointerSystem.RaycastAll(pointer, hits);

        // Only the frontmost UI graphic can receive the click.
        Button target = hits.Count > 0 ? hits[0].gameObject.GetComponentInParent<Button>() : null;
        if (!Eligible(target)) target = null;
        SetHovered(target);

        if (target != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            ButtonState state = GetState(target);
            if (state == null) return;
            state.tween?.Kill();
            float half = punchDuration * 0.5f;
            state.tween = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => state.factor, value => SetScale(state, value),
                    pressedScale, half).SetEase(Ease.OutCubic))
                .Append(DOTween.To(() => state.factor, value => SetScale(state, value),
                    hoverScale, half).SetEase(Ease.OutBack));
        }
    }

    private bool Eligible(Button button)
    {
        if (button == null || !button.isActiveAndEnabled || !button.IsInteractable() ||
            !button.transform.IsChildOf(transform) || button.GetComponent<HoverPunchButton>() != null)
            return false;

        if (excludedButtons != null)
            foreach (Button excluded in excludedButtons)
                if (excluded == button) return false;

        return true;
    }

    private ButtonState GetState(Button button)
    {
        if (states.TryGetValue(button, out ButtonState state)) return state;
        RectTransform rect = button.transform as RectTransform;
        if (rect == null || rect.localScale.sqrMagnitude < 0.01f) return null;
        state = new ButtonState { rect = rect, baseScale = rect.localScale };
        states.Add(button, state);
        return state;
    }

    private void SetHovered(Button target)
    {
        if (hovered == target) return;
        if (hovered != null && states.TryGetValue(hovered, out ButtonState oldState))
            Animate(oldState, 1f);
        hovered = target;
        if (target != null)
        {
            ButtonState state = GetState(target);
            if (state != null) Animate(state, hoverScale);
        }
    }

    private void Animate(ButtonState state, float scale)
    {
        state.tween?.Kill();
        state.tween = DOTween.To(() => state.factor, value => SetScale(state, value),
            scale, hoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    private static void SetScale(ButtonState state, float factor)
    {
        if (state.rect == null) return;
        // Preserve the visible center even when the button's pivot is off-center.
        Vector3 center = state.rect.TransformPoint(state.rect.rect.center);
        state.factor = factor;
        state.rect.localScale = state.baseScale * factor;
        state.rect.position += center - state.rect.TransformPoint(state.rect.rect.center);
    }

    private void OnDisable()
    {
        foreach (ButtonState state in states.Values)
        {
            state.tween?.Kill();
            SetScale(state, 1f);
        }
        states.Clear();
        hovered = null;
    }
}
