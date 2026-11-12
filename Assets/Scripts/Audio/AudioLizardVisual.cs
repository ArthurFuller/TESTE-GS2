using UnityEngine;

// O botao continua controlado por AudioSettingsUI; aqui so trocamos o estado visual.
public sealed class AudioLizardVisual : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private bool? muted;

    public void SetMuted(bool value)
    {
        if (animator == null || muted == value) return;
        muted = value;
        animator.Play(value ? "Desligando" : "Tocando", 0, 0f);
    }
}
