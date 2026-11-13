using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class GatheringVisitButtonVisual : MonoBehaviour
{
    [SerializeField] private RectTransform character;
    [Header("Botão")]
    [SerializeField] private Vector2 buttonPosition = new Vector2(18f, 80f);
    [SerializeField] private Vector2 buttonSize = new Vector2(94f, 82f);
    [SerializeField, Min(0f)] private float entranceDuration = 0.4f;
    [SerializeField, Min(0f)] private float offscreenPadding = 12f;
    [Header("Personagem")]
    [SerializeField] private Vector2 characterPosition = new Vector2(0f, -8f);
    [SerializeField] private Vector2 characterSize = new Vector2(72f, 72f);
    [Header("Saltinho lateral")]
    [SerializeField, Min(0f)] private float sideHopDistance = 12f;
    [SerializeField, Min(0f)] private float sideHopDuration = 0.16f;
    [SerializeField, Min(0.05f)] private float exitDuration = 0.18f;

    private RectTransform button;
    private Sequence entrance;
    public RectTransform Character => character;
    public bool IsExiting { get; private set; }

    private void OnEnable()
    {
        button = transform as RectTransform;
        if (button == null) return;
        ApplyLayout();
        if (!Application.isPlaying) return;
        IsExiting = false;

        Animator animator = character != null ? character.GetComponent<Animator>() : null;
        if (animator != null) animator.Play("Idle", 0, 0f);
        button.anchoredPosition = new Vector2(-buttonSize.x - offscreenPadding,
            buttonPosition.y);
        entrance = DOTween.Sequence().SetUpdate(true).SetTarget(this)
            .Append(button.DOAnchorPos(buttonPosition + Vector2.right * sideHopDistance,
                entranceDuration).SetEase(Ease.OutCubic))
            .Append(button.DOAnchorPos(buttonPosition, sideHopDuration).SetEase(Ease.OutBack));
    }

    private void OnDisable()
    {
        IsExiting = false;
        entrance?.Kill();
        if (button != null) button.DOKill();
        if (character != null) character.DOKill();
    }

    private void ApplyLayout()
    {
        button ??= transform as RectTransform;
        if (button == null) return;
        button.sizeDelta = buttonSize;
        button.anchoredPosition = buttonPosition;
        if (character == null) return;
        character.sizeDelta = characterSize;
        character.anchoredPosition = characterPosition;
        Image image = character.GetComponent<Image>();
        if (image != null) image.raycastTarget = false;
    }

    public void RefreshLayout() => ApplyLayout();

    public void ExitLeftFast()
    {
        if (!Application.isPlaying || button == null || IsExiting) return;
        IsExiting = true;
        entrance?.Kill();
        button.DOKill();
        button.DOAnchorPosX(-buttonSize.x - offscreenPadding, exitDuration)
            .SetEase(Ease.InCubic).SetUpdate(true)
            .OnComplete(() => gameObject.SetActive(false));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyInspectorLayout;
        UnityEditor.EditorApplication.delayCall += ApplyInspectorLayout;
    }

    private void ApplyInspectorLayout()
    {
        if (this == null) return;
        if (Application.isPlaying) entrance?.Kill();
        else if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        ApplyLayout();
        UnityEditor.SceneView.RepaintAll();
    }
#endif
}
