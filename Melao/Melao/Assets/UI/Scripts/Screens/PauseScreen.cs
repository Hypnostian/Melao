using UnityEngine;

public class PauseScreen : MonoBehaviour
{
    public void OnResumePressed()
    {
        UIManager.Instance.ClosePause();
    }

    public void OnSettingsPressed()
    {
        UIManager.Instance.ShowScreen("Settings");
    }

    public void OnExitToMenuPressed()
    {
        Time.timeScale = 1f;
        SceneLoader.Instance.LoadScene("MainMenu");
    }
}