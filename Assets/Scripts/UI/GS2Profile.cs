using UnityEngine;
using UnityEngine.UI;

public sealed class GS2Profile : MonoBehaviour
{
    private const string NicknameKey = "GS2.LeaderboardNickname";
    public static string Nickname => PlayerPrefs.GetString(NicknameKey, string.Empty);
    public static bool HasNickname => !string.IsNullOrWhiteSpace(Nickname);

    [SerializeField] private InputField nicknameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Toggle skipTutorial;
    [SerializeField] private Text notice;

    private void Awake()
    {
        if (nicknameInput != null) nicknameInput.text = Nickname;
        if (skipTutorial != null)
        {
            skipTutorial.isOn = InitialTutorial.SkipRequested;
            skipTutorial.onValueChanged.AddListener(SetSkipTutorial);
        }
        if (saveButton != null) saveButton.onClick.AddListener(SaveNickname);
    }
    private void OnDestroy()
    {
        if (skipTutorial != null) skipTutorial.onValueChanged.RemoveListener(SetSkipTutorial);
        if (saveButton != null) saveButton.onClick.RemoveListener(SaveNickname);
    }

    private void SetSkipTutorial(bool value) => InitialTutorial.SkipRequested = value;

    public void SaveNickname()
    {
        if (nicknameInput == null) return;
        string value = nicknameInput.text.Trim();
        if (value.Length < 2 || value.Length > 18)
        {
            if (notice != null) notice.text = "Nickname: use entre 2 e 18 caracteres.";
            return;
        }
        PlayerPrefs.SetString(NicknameKey, value);
        PlayerPrefs.Save();
        if (notice != null) notice.text = "Nickname salvo neste dispositivo.";
    }
}
