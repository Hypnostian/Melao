using UnityEngine;

public class GameOverScreen : MonoBehaviour
{
    public void OnRetryPressed()
    {
        Time.timeScale = 1f;
        SceneLoader.Instance.ReloadCurrentScene();
    }

    public void OnExitToMenuPressed()
    {
        Time.timeScale = 1f;
        SceneLoader.Instance.LoadScene("MainMenu");
    }
}