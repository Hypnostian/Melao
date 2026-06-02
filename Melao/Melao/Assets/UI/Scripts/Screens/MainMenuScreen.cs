using UnityEngine;

public class MainMenuScreen : MonoBehaviour
{
    private const string FirstLevel = "Nivel 1 Quipto";

    [SerializeField] private AudioClip menuMusic;

    private void OnEnable()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMusic(menuMusic);
    }

    public void OnPlayPressed()
    {
        string last = SaveSystem.GetLastLevel();
        if (!string.IsNullOrEmpty(last))
            SceneLoader.Instance.LoadScene(last);
        else
            SceneLoader.Instance.LoadScene(FirstLevel);
    }

    public void OnNewGamePressed()
    {
        SaveData data = SaveSystem.Load();
        float music = data.musicVolume;
        float sfx = data.sfxVolume;
        bool vib = data.vibration;

        SaveSystem.DeleteSave();

        SaveSystem.SaveSettings(music, sfx, vib);
        UIManager.Instance.ShowScreen("Cutscene");
    }

    public void OnSettingsPressed()
    {
        var settings = FindFirstObjectByType<SettingsScreen>(FindObjectsInactive.Include);
        if (settings != null) settings.SetReturnScreen("MainMenu");
        UIManager.Instance.ShowScreen("Settings");
    }

    public void OnCreditsPressed()
    {
        UIManager.Instance.ShowScreen("Credits");
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }
}