using UnityEngine;

public class MainMenuScreen : MonoBehaviour
{
    public void OnPlayPressed()
    {
        // Ir a selección de región/nivel
        SceneLoader.Instance.LoadScene("RegionSelect");
    }

    public void OnSettingsPressed()
    {
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