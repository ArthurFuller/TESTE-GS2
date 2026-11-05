using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ResourceCounterHover : MonoBehaviour
{
    [Serializable]
    private struct Counter
    {
        [Tooltip("Referência apenas para identificar o recurso no Inspector.")]
        public RectTransform icon;
        [Tooltip("Referência apenas para identificar o contador no Inspector.")]
        public RectTransform number;
        [Tooltip("Imagem de fundo criada e posicionada no Editor.")]
        public Image background;
    }

    [SerializeField] private Counter[] counters;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.15f;

    private float[] visibleAlpha;
    private int hovered = -1;
    private Camera uiCamera;

    private void Awake()
    {
        if (counters == null) return;
        var canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        visibleAlpha = new float[counters.Length];
        for (int i = 0; i < counters.Length; i++)
        {
            Image background = counters[i].background;
            if (background == null) continue;
            visibleAlpha[i] = background.color.a;
            background.raycastTarget = false;
            SetAlpha(background, 0f);
        }
    }

    private void Update()
    {
        if (Mouse.current == null || visibleAlpha == null) return;
        Vector2 pointer = Mouse.current.position.ReadValue();
        int current = -1;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < counters.Length; i++)
        {
            Image background = counters[i].background;
            if (background == null || !RectTransformUtility.RectangleContainsScreenPoint(
                    background.rectTransform, pointer, uiCamera)) continue;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(uiCamera,
                background.rectTransform.TransformPoint(background.rectTransform.rect.center));
            float distance = (pointer - center).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            current = i;
        }

        if (current == hovered) return;
        Fade(hovered, false);
        hovered = current;
        Fade(hovered, true);
    }

    private void Fade(int index, bool show)
    {
        if (index < 0 || index >= counters.Length) return;
        Image background = counters[index].background;
        if (background == null) return;
        background.DOKill();
        background.DOFade(show ? visibleAlpha[index] : 0f, fadeDuration);
    }

    private void OnDisable()
    {
        if (visibleAlpha == null) return;
        for (int i = 0; i < counters.Length; i++)
        {
            Image background = counters[i].background;
            if (background == null) continue;
            background.DOKill();
            SetAlpha(background, 0f);
        }
        hovered = -1;
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
