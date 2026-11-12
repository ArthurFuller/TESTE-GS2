using UnityEngine;
using UnityEngine.UI;

public sealed class AudioLizardVisual : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Image image;
    [SerializeField] private Sprite closedEyeSprite;

    private const float ClosedEyeTime = 0.5f;
    private bool? muted;

    private void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (closedEyeSprite == null && image != null) closedEyeSprite = image.sprite;
        if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    public void SetMuted(bool value)
    {
        if (animator == null || image == null || muted == value) return;
        muted = value;
        image.enabled = true;
        animator.enabled = !value;
        if (value) image.sprite = closedEyeSprite;
        else animator.Play("Tocando", 0, ClosedEyeTime);
    }
}
