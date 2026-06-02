using UnityEngine;

public class LevelCompleteScreen : MonoBehaviour
{
    public void OnNextLevelPressed()
    {
        Time.timeScale = 1f;
        string next = SaveSystem.GetNextLevel();
        if (!string.IsNullOrEmpty(next))
        {
            SceneLoader.Instance.LoadScene(next);
        }
        else
        {
            SceneLoader.Instance.UnloadCurrentScene();
            UIManager.Instance.ShowScreen("MainMenu");
        }
    }

    public void OnExitToMenuPressed()
    {
        Time.timeScale = 1f;
        UIManager.Instance.ShowScreen("MainMenu");
        SceneLoader.Instance.UnloadCurrentScene();
    }
}