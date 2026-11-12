using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// O Animator troca os quadros; o tween espreme o lagarto atrás da placa.
public sealed class AudioLizardVisual : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Image image;
    [SerializeField] private RectTransform musicBox;
    [SerializeField, Min(0.1f)] private float transitionDuration = 0.5f;

    private RectTransform rect;
    private Vector3 visibleScale;
    private Sequence transition;
    private bool? muted;

    private void Awake()
    {
        rect = (RectTransform)transform;
        if (image == null) image = GetComponent<Image>();
        visibleScale = rect.localScale;
        if (musicBox != null) musicBox.SetAsLastSibling();
        if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    public void SetMuted(bool value)
    {
        if (animator == null || image == null || musicBox == null || muted == value) return;
        transition?.Kill();
        bool firstState = !muted.HasValue;
        muted = value;

        if (firstState)
        {
            rect.localScale = new Vector3(visibleScale.x,
                value ? 0f : visibleScale.y, visibleScale.z);
            image.enabled = !value;
            animator.enabled = !value;
            if (!value) animator.Play("Tocando", 0, 0f);
            return;
        }

        image.enabled = true;
        animator.enabled = true;
        animator.Play(value ? "Desligando" : "Ligando", 0, 0f);
        transition = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        transition.Append(rect.DOScaleY(value ? 0f : visibleScale.y,
            transitionDuration).SetEase(Ease.InOutSine));
        transition.OnComplete(() =>
        {
            if (muted != value) return;
            if (value)
            {
                image.enabled = false;
                animator.enabled = false;
            }
            else animator.Play("Tocando", 0, 0f);
        });
    }

    private void OnDestroy() => transition?.Kill();
}
