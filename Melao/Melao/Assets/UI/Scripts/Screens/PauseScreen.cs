using UnityEngine;

public class PauseScreen : MonoBehaviour
{
    public void OnResumePressed()
    {
        UIManager.Instance.ClosePause();
    }

    public void OnSettingsPressed()
    {
        var settings = FindFirstObjectByType<SettingsScreen>(FindObjectsInactive.Include);
        if (settings != null) settings.SetReturnScreen("Pause");
        UIManager.Instance.ShowScreen("Settings");
    }

    public void OnExitToMenuPressed()
    {
        Time.timeScale = 1f;
        UIManager.Instance.ShowScreen("MainMenu");
        SceneLoader.Instance.UnloadCurrentScene();
    }
}
