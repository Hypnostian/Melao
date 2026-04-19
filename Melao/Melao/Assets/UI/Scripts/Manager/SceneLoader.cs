using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    // Tiempo mínimo que se muestra la pantalla de carga (en segundos)
    [SerializeField] private float minLoadTime = 1.5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else Destroy(gameObject);
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadAsync(sceneName));
    }

    public void ReloadCurrentScene()
    {
        string current = SceneManager.GetActiveScene().name;
        LoadScene(current);
    }

    private IEnumerator LoadAsync(string sceneName)
    {
        Time.timeScale = 1f;

        // TODO: activar pantalla de carga aquí cuando esté lista
        // UIManager.Instance.ShowScreen("Loading");

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        float elapsed = 0f;

        while (!op.isDone)
        {
            elapsed += Time.deltaTime;

            // Esperar a que cargue Y a que pase el tiempo mínimo
            if (op.progress >= 0.9f && elapsed >= minLoadTime)
            {
                op.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}