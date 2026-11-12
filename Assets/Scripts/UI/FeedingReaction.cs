using UnityEngine;
using UnityEngine.UI;

// Visual previamente colocado no card; a lógica só escolhe a arte e chama o Animator.
public sealed class FeedingReaction : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Animator animator;
    [SerializeField] private Sprite preparedFoodIcon;
    [SerializeField] private Sprite gatheredFoodIcon;

    public void ResetVisual()
    {
        if (animator != null) animator.enabled = false;
        if (image != null)
        {
            Color color = image.color;
            color.a = 0f;
            image.color = color;
        }
    }

    public void Show(bool prepared)
    {
        if (image == null || animator == null) return;
        image.sprite = prepared ? preparedFoodIcon : gatheredFoodIcon;
        animator.enabled = true;
        animator.Play("Feedback", 0, 0f);
    }
}
