using UnityEngine;

public class LevelCompleteScreen : MonoBehaviour
{
    // El nivel siguiente se pasa desde el GameManager al completar
    private string nextSceneName;

    public void SetNextLevel(string sceneName)
    {
        nextSceneName = sceneName;
    }

    public void OnNextLevelPressed()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneLoader.Instance.LoadScene(nextSceneName);
        else
            SceneLoader.Instance.LoadScene("RegionSelect");
    }

    public void OnExitToMenuPressed()
    {
        Time.timeScale = 1f;
        SceneLoader.Instance.LoadScene("MainMenu");
    }
}