using UnityEngine;

public class GameCompleteScreen : MonoBehaviour
{
    public void OnBackToMenuPressed()
    {
        Time.timeScale = 1f;
        UIManager.Instance.ShowScreen("MainMenu");
        SceneLoader.Instance.UnloadCurrentScene();
    }
}

