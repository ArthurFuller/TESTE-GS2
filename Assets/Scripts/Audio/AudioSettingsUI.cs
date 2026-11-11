using UnityEngine;
using UnityEngine.UI;

// Controles na tela de pausa; volume 0 desliga sem apagar a preferência de mute.
public sealed class AudioSettingsUI : MonoBehaviour
{
    [SerializeField] private Button musicLess;
    [SerializeField] private Button musicMore;
    [SerializeField] private Button musicMute;
    [SerializeField] private Text musicValue;
    [SerializeField] private Text musicMuteLabel;
    [SerializeField] private Button effectsLess;
    [SerializeField] private Button effectsMore;
    [SerializeField] private Button effectsMute;
    [SerializeField] private Text effectsValue;
    [SerializeField] private Text effectsMuteLabel;

    private const float Step = 0.1f;

    private void Awake()
    {
        if (musicLess != null) musicLess.onClick.AddListener(DecreaseMusic);
        if (musicMore != null) musicMore.onClick.AddListener(IncreaseMusic);
        if (musicMute != null) musicMute.onClick.AddListener(ToggleMusic);
        if (effectsLess != null) effectsLess.onClick.AddListener(DecreaseEffects);
        if (effectsMore != null) effectsMore.onClick.AddListener(IncreaseEffects);
        if (effectsMute != null) effectsMute.onClick.AddListener(ToggleEffects);
        Refresh();
    }

    private void OnEnable() => Refresh();
    private void Start() => Refresh();

    private void OnDestroy()
    {
        if (musicLess != null) musicLess.onClick.RemoveListener(DecreaseMusic);
        if (musicMore != null) musicMore.onClick.RemoveListener(IncreaseMusic);
        if (musicMute != null) musicMute.onClick.RemoveListener(ToggleMusic);
        if (effectsLess != null) effectsLess.onClick.RemoveListener(DecreaseEffects);
        if (effectsMore != null) effectsMore.onClick.RemoveListener(IncreaseEffects);
        if (effectsMute != null) effectsMute.onClick.RemoveListener(ToggleEffects);
    }

    private void DecreaseMusic() => ChangeMusic(-Step);
    private void IncreaseMusic() => ChangeMusic(Step);
    private void DecreaseEffects() => ChangeEffects(-Step);
    private void IncreaseEffects() => ChangeEffects(Step);
    private void ToggleMusic()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMuted(!AudioManager.Instance.MusicMuted);
        Refresh();
    }
    private void ToggleEffects()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetEffectsMuted(!AudioManager.Instance.EffectsMuted);
        Refresh();
    }
    private void ChangeMusic(float delta)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(Mathf.Round((AudioManager.Instance.MusicVolume + delta) * 10f) / 10f);
        Refresh();
    }
    private void ChangeEffects(float delta)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetEffectsVolume(Mathf.Round((AudioManager.Instance.EffectsVolume + delta) * 10f) / 10f);
        Refresh();
    }
    private void Refresh()
    {
        AudioManager a = AudioManager.Instance;
        if (a == null) return;
        if (musicValue != null) musicValue.text = Mathf.RoundToInt(a.MusicVolume * 100f) + "%";
        if (effectsValue != null) effectsValue.text = Mathf.RoundToInt(a.EffectsVolume * 100f) + "%";
        if (musicMuteLabel != null) musicMuteLabel.text = a.MusicMuted ? "ATIVAR" : "SILENCIAR";
        if (effectsMuteLabel != null) effectsMuteLabel.text = a.EffectsMuted ? "ATIVAR" : "SILENCIAR";
    }
}
