using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Transicion de pantalla (fundido a negro) reutilizable y AUTOSUFICIENTE: crea su
// propio Canvas en overlay (DontDestroyOnLoad), asi funciona en cualquier escena
// sin necesidad de tenerlo puesto a mano. Usa tiempo NO escalado, asi que el
// fundido corre aunque Time.timeScale sea 0.
//
// Uso tipico (LevelEndTrigger): ScreenFader.GetOrCreate().FadeToLevel(next, current);
//   -> funde a negro, carga el siguiente nivel y vuelve a fundir desde negro.
[DisallowMultipleComponent]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private Color fadeColor = Color.black;

    private CanvasGroup group;
    private bool busy;

    public static ScreenFader GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("ScreenFader");
        return go.AddComponent<ScreenFader>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;                 // por encima de todo (incluido HUD)
        gameObject.AddComponent<GraphicRaycaster>();

        var imgGo = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        imgGo.transform.SetParent(transform, false);
        var img = imgGo.GetComponent<Image>();
        img.color = fadeColor;
        img.raycastTarget = true;                   // bloquea clicks durante la transicion

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        group = imgGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    // Funde a negro -> marca completado y carga el siguiente nivel -> funde de vuelta.
    // Si no hay siguiente nivel, dispara GameComplete.
    public void FadeToLevel(string nextLevel, string currentLevel)
    {
        if (busy) return;
        StartCoroutine(TransitionRoutine(nextLevel, currentLevel));
    }

    private IEnumerator TransitionRoutine(string nextLevel, string currentLevel)
    {
        busy = true;
        group.blocksRaycasts = true;

        yield return Fade(0f, 1f);                  // a negro

        if (!string.IsNullOrEmpty(currentLevel))
            SaveSystem.MarkLevelComplete(currentLevel);

        if (!string.IsNullOrEmpty(nextLevel))
        {
            SaveSystem.SaveNextLevel(nextLevel);
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadScene(nextLevel);          // carga aditiva del juego
            else
                SceneManager.LoadScene(nextLevel);                  // fallback (escena suelta)

            // Dar tiempo a que cargue la nueva escena detras del negro.
            yield return new WaitForSecondsRealtime(0.4f);
        }
        else
        {
            // Sin siguiente nivel: fin del juego.
            UIManager.Instance?.TriggerGameComplete();
            busy = false;
            group.blocksRaycasts = false;
            yield break;                            // se queda en negro tras la pantalla final
        }

        yield return Fade(1f, 0f);                  // revela el nuevo nivel
        group.blocksRaycasts = false;
        busy = false;
    }

    // Fundido simple (puede usarse para cutscenes). Tiempo no escalado.
    public Coroutine FadeOut() => StartCoroutine(Fade(0f, 1f));
    public Coroutine FadeIn()  => StartCoroutine(Fade(1f, 0f));

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        group.alpha = from;
        float dur = Mathf.Max(0.01f, fadeDuration);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        group.alpha = to;
    }
}
