using System.Collections.Generic;
using UnityEngine;

// Dicas em contexto, sem modal adicional; não bloqueia cartas ou personagens.
public sealed class TutorialHints : MonoBehaviour
{
    public static TutorialHints Instance { get; private set; }
    [SerializeField] private GameFeedback feedback;
    private readonly HashSet<string> seen = new();

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public static void Once(string key, string message)
    {
        if (Instance == null || InitialTutorial.SkipRequested ||
            Instance.seen.Contains(key)) return;
        Instance.seen.Add(key);
        // O mesmo aviso não reaparece após reabrir a partida.
        if (PlayerPrefs.GetInt("GS2.Hint." + key, 0) != 0) return;
        PlayerPrefs.SetInt("GS2.Hint." + key, 1);
        if (Instance.feedback != null) Instance.feedback.ShowMessage(message);
    }
}
