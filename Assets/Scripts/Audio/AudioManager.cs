using System;
using System.Collections.Generic;
using UnityEngine;

// Biblioteca configurável no Inspector. Não cria objetos nem gera áudio.
public sealed class AudioManager : MonoBehaviour
{
    [Serializable]
    private sealed class CueEntry
    {
        public SoundCue cue;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(0f)] public float minInterval = 0.06f;
    }

    [Header("Fontes na Hierarchy")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource fishingPullSource;
    [Header("Puxar linha (pesca)")]
    [SerializeField, Min(0.1f)] private float fishingIdleDelay = 1f;
    [SerializeField, Range(0.02f, 0.4f)] private float fishingFadeDuration = 0.12f;
    [Header("Biblioteca de efeitos (atribuir clips aqui)")]
    [SerializeField] private CueEntry[] effects = Array.Empty<CueEntry>();

    private const string MusicKey = "Sertao_MusicVolume_v1";
    private const string EffectsKey = "Sertao_EffectsVolume_v1";
    private const string MusicMuteKey = "Sertao_MusicMute_v1";
    private const string EffectsMuteKey = "Sertao_EffectsMute_v1";
    private readonly Dictionary<SoundCue, CueEntry> entries = new();
    private readonly Dictionary<SoundCue, float> lastPlay = new();
    private readonly Dictionary<SoundCue, int> lastVariant = new();
    private float musicVolume;
    private float effectsVolume;
    private bool musicMuted;
    private bool effectsMuted;
    private float fishingPullUntil;
    private float fishingPullFade;
    private float fishingPullCueVolume = 1f;

    public static AudioManager Instance { get; private set; }
    public float MusicVolume => musicVolume;
    public float EffectsVolume => effectsVolume;
    public bool MusicMuted => musicMuted;
    public bool EffectsMuted => effectsMuted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("AudioManager duplicado: mantendo o primeiro", this);
            enabled = false;
            return;
        }
        Instance = this;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.7f));
        effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, 0.85f));
        musicMuted = PlayerPrefs.GetInt(MusicMuteKey, 0) != 0;
        effectsMuted = PlayerPrefs.GetInt(EffectsMuteKey, 0) != 0;
        foreach (CueEntry entry in effects)
            if (entry != null && !entries.ContainsKey(entry.cue))
                entries.Add(entry.cue, entry);
        if (musicSource != null) { musicSource.loop = true; musicSource.spatialBlend = 0f; }
        if (effectsSource != null) { effectsSource.loop = false; effectsSource.spatialBlend = 0f; }
        if (ambienceSource != null) { ambienceSource.loop = true; ambienceSource.spatialBlend = 0f; }
        if (fishingPullSource != null)
        {
            fishingPullSource.loop = true;
            fishingPullSource.playOnAwake = false;
            fishingPullSource.spatialBlend = 0f;
            if (entries.TryGetValue(SoundCue.FishingPull, out CueEntry pull))
            {
                fishingPullCueVolume = pull.volume;
                if (fishingPullSource.clip == null && pull.clips != null)
                    foreach (AudioClip clip in pull.clips)
                        if (clip != null) { fishingPullSource.clip = clip; break; }
            }
            fishingPullSource.Stop();
        }
        ApplyVolumes();
    }

    private void Start()
    {
        // A música existente pode ser atribuída diretamente no AudioSource da cena.
        if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
            musicSource.Play();
        if (ambienceSource != null && ambienceSource.clip != null && !ambienceSource.isPlaying)
            ambienceSource.Play();
    }

    // A pesca mantém um único som em loop enquanto o jogador continua puxando.
    public static void KeepFishingPullAlive()
    {
        if (Instance == null || !Instance.isActiveAndEnabled) return;
        Instance.RefreshFishingPull();
    }

    public static void StopFishingPull()
    {
        if (Instance != null) Instance.fishingPullUntil = 0f;
    }

    private void RefreshFishingPull()
    {
        if (fishingPullSource == null || fishingPullSource.clip == null ||
            effectsMuted || effectsVolume <= 0f || Time.timeScale <= 0f) return;
        fishingPullUntil = Time.unscaledTime + fishingIdleDelay;
        if (!fishingPullSource.isPlaying) fishingPullSource.Play();
    }

    private void Update()
    {
        if (fishingPullSource == null) return;
        if (Time.timeScale <= 0f) fishingPullUntil = 0f;
        bool active = Time.unscaledTime < fishingPullUntil && !effectsMuted && effectsVolume > 0f;
        fishingPullFade = Mathf.MoveTowards(fishingPullFade, active ? 1f : 0f,
            Time.unscaledDeltaTime / Mathf.Max(0.02f, fishingFadeDuration));
        fishingPullSource.volume = fishingPullFade * fishingPullCueVolume *
            (effectsMuted ? 0f : effectsVolume);
        if (!active && fishingPullFade <= 0f && fishingPullSource.isPlaying)
            fishingPullSource.Stop();
    }

    public static void Play(SoundCue cue)
    {
        if (Instance != null && Instance.isActiveAndEnabled)
            Instance.PlayEffect(cue);
    }

    private void PlayEffect(SoundCue cue)
    {
        if (effectsSource == null || effectsMuted || effectsVolume <= 0f ||
            !entries.TryGetValue(cue, out CueEntry entry) || entry.clips == null || entry.clips.Length == 0)
            return;
        float now = Time.unscaledTime;
        if (lastPlay.TryGetValue(cue, out float last) && now - last < entry.minInterval)
            return;
        int first = UnityEngine.Random.Range(0, entry.clips.Length);
        int pick = first;
        // Evita repetição imediata de um mesmo sample quando há variações.
        if (entry.clips.Length > 1 && lastVariant.TryGetValue(cue, out int previous) && first == previous)
            pick = (first + 1) % entry.clips.Length;
        AudioClip clip = entry.clips[pick];
        if (clip == null) return;
        lastPlay[cue] = now;
        lastVariant[cue] = pick;
        effectsSource.PlayOneShot(clip, Mathf.Clamp01(entry.volume));
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, musicVolume);
        ApplyVolumes();
    }
    public void SetEffectsVolume(float value)
    {
        effectsVolume = Mathf.Clamp01(value);
        if (effectsVolume <= 0f) StopFishingPull();
        PlayerPrefs.SetFloat(EffectsKey, effectsVolume);
        ApplyVolumes();
    }
    public void SetMusicMuted(bool muted)
    {
        musicMuted = muted;
        PlayerPrefs.SetInt(MusicMuteKey, muted ? 1 : 0);
        ApplyVolumes();
    }
    public void SetEffectsMuted(bool muted)
    {
        effectsMuted = muted;
        if (muted) StopFishingPull();
        PlayerPrefs.SetInt(EffectsMuteKey, muted ? 1 : 0);
        ApplyVolumes();
    }
    private void ApplyVolumes()
    {
        if (musicSource != null) musicSource.volume = musicMuted ? 0f : musicVolume;
        if (effectsSource != null) effectsSource.volume = effectsMuted ? 0f : effectsVolume;
        if (ambienceSource != null) ambienceSource.volume = effectsMuted ? 0f : effectsVolume;
        if (fishingPullSource != null)
            fishingPullSource.volume = fishingPullFade * fishingPullCueVolume *
                (effectsMuted ? 0f : effectsVolume);
    }
    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        StopFishingPull();
        if (fishingPullSource != null) fishingPullSource.Stop();
        fishingPullFade = 0f;
        PlayerPrefs.Save();
    }
    private void OnDisable()
    {
        if (fishingPullSource != null) fishingPullSource.Stop();
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
