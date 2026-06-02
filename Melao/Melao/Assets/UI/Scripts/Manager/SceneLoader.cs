using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    private string currentGameplayScene;
    private GameObject uiCamera;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            uiCamera = GameObject.Find("UICamera");
        }
        else Destroy(gameObject);
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadAsync(sceneName));
    }

    public void ReloadCurrentScene()
    {
        if (!string.IsNullOrEmpty(currentGameplayScene))
            LoadScene(currentGameplayScene);
    }

    public void UnloadCurrentScene()
    {
        if (!string.IsNullOrEmpty(currentGameplayScene))
            StartCoroutine(UnloadCurrentAsync());
    }

    private IEnumerator UnloadCurrentAsync()
    {
        var op = SceneManager.UnloadSceneAsync(currentGameplayScene);
        if (op != null) yield return op;
        currentGameplayScene = null;
        if (uiCamera != null) uiCamera.SetActive(true);
    }

    private IEnumerator LoadAsync(string sceneName)
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(currentGameplayScene))
        {
            var unloadOp = SceneManager.UnloadSceneAsync(currentGameplayScene);
            if (unloadOp != null) yield return unloadOp;
            currentGameplayScene = null;
        }

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        currentGameplayScene = sceneName;
        SaveSystem.SaveLastLevel(sceneName);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

        if (uiCamera != null) uiCamera.SetActive(false);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowScreen("HUD");

        HUDController.Instance?.InitHearts(3, 3);
        ControlRebinder.Instance?.ApplySavedOverrides();
    }
}
