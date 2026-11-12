using UnityEngine;
using UnityEngine.UI;

// Botao da barra superior: o Animator da arte reage ao estado geral do som.
public sealed class AudioLizardButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private AudioLizardVisual visual;

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(ToggleSound);
    }

    private void Start() => Refresh();

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(ToggleSound);
    }

    private void ToggleSound()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;
        bool mute = !(audio.MusicMuted && audio.EffectsMuted);
        audio.SetMusicMuted(mute);
        audio.SetEffectsMuted(mute);
        Refresh();
    }

    private void Refresh()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio != null && visual != null)
            visual.SetMuted(audio.MusicMuted && audio.EffectsMuted);
    }
}
