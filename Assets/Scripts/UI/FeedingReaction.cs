using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class FeedingReaction : MonoBehaviour
{
    [System.Serializable]
    private struct Motion
    {
        public Vector2 start;
        public Vector2 end;
        public Vector2 size;
        [Min(0.05f)] public float duration;
    }

    [SerializeField] private Image image;
    [SerializeField] private Animator animator;
    [SerializeField] private Sprite preparedFoodIcon;
    [SerializeField] private Sprite gatheredFoodIcon;
    [Header("Coração - comida pronta")]
    [SerializeField] private Motion heart = new Motion
    {
        start = new Vector2(0f, 30.6f), end = new Vector2(0f, 67f),
        size = new Vector2(70f, 70f), duration = 0.8f
    };
    [Header("Joinha - demais alimentos")]
    [SerializeField] private Motion thumbsUp = new Motion
    {
        start = new Vector2(0f, 30.6f), end = new Vector2(0f, 67f),
        size = new Vector2(70f, 70f), duration = 0.8f
    };
    [Header("Prévia no Editor")]
    [SerializeField] private bool previewHeart;
    [SerializeField] private bool previewThumbsUp;

    private Sequence motion;

    public void CopyStyleFrom(FeedingReaction source)
    {
        if (source == null || source == this) return;
        heart = source.heart;
        thumbsUp = source.thumbsUp;
        preparedFoodIcon = source.preparedFoodIcon;
        gatheredFoodIcon = source.gatheredFoodIcon;
    }

    private void OnValidate()
    {
        if (Application.isPlaying || image == null) return;
        if (animator != null) animator.enabled = false;
        if (previewHeart || previewThumbsUp)
        {
            bool prepared = previewHeart;
            Motion style = prepared ? heart : thumbsUp;
            image.sprite = prepared ? preparedFoodIcon : gatheredFoodIcon;
            image.rectTransform.anchoredPosition = style.start;
            image.rectTransform.sizeDelta = style.size;
            image.rectTransform.localScale = Vector3.one;
            image.color = Color.white;
        }
        else
        {
            Color color = image.color;
            color.a = 0f;
            image.color = color;
        }
    }

    public void ResetVisual()
    {
        motion?.Kill();
        if (animator != null) animator.enabled = false;
        if (image == null) return;
        image.DOKill();
        image.rectTransform.DOKill();
        image.rectTransform.localScale = Vector3.one;
        image.raycastTarget = false;
        Color color = image.color;
        color.a = 0f;
        image.color = color;
    }

    public void Show(bool prepared)
    {
        if (image == null) return;
        ResetVisual();
        Motion style = prepared ? heart : thumbsUp;
        RectTransform rect = image.rectTransform;
        image.sprite = prepared ? preparedFoodIcon : gatheredFoodIcon;
        rect.anchoredPosition = style.start;
        rect.sizeDelta = style.size;
        rect.localScale = Vector3.one * 0.8f;
        float duration = Mathf.Max(0.05f, style.duration);
        motion = DOTween.Sequence().SetTarget(this)
            .Append(rect.DOAnchorPos(style.end, duration).SetEase(Ease.OutCubic))
            .Join(rect.DOScale(1f, duration * 0.28f).SetEase(Ease.OutBack))
            .Join(image.DOFade(1f, duration * 0.22f))
            .Insert(duration * 0.65f, image.DOFade(0f, duration * 0.35f));
    }
}
