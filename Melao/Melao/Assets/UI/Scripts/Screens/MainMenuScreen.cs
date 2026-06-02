using UnityEngine;

public class MainMenuScreen : MonoBehaviour
{
    private const string FirstLevel = "Nivel 4 Choco-Lala";

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
        SaveSystem.DeleteSave();
        SceneLoader.Instance.LoadScene(FirstLevel);
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