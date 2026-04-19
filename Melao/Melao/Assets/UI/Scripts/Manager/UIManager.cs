using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // — Pantallas —
    [SerializeField] private GameObject mainMenuScreen;
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private GameObject settingsScreen;
    [SerializeField] private GameObject gameOverScreen;
    [SerializeField] private GameObject levelCompleteScreen;
    [SerializeField] private GameObject hud;

    // Pantalla activa actualmente
    private GameObject currentScreen;

    private void Awake()
    {
        // Singleton: solo existe un UIManager en toda la partida
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            HideAllScreens();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Extraer HideAllScreens como método privado reutilizable
    private void HideAllScreens()
    {
        mainMenuScreen.SetActive(false);
        pauseScreen.SetActive(false);
        settingsScreen.SetActive(false);
        gameOverScreen.SetActive(false);
        levelCompleteScreen.SetActive(false);
        hud.SetActive(false);
    }

    private void Start()
    {
        // Al iniciar, mostrar solo el menú principal
        ShowScreen("MainMenu");
    }

    public void ShowScreen(string screenName)
    {
        // Ocultar pantalla actual antes de mostrar la nueva
        if (currentScreen != null)
            currentScreen.SetActive(false);

        GameObject next = screenName switch
        {
            "MainMenu"      => mainMenuScreen,
            "Pause"         => pauseScreen,
            "Settings"      => settingsScreen,
            "GameOver"      => gameOverScreen,
            "LevelComplete" => levelCompleteScreen,
            "HUD"           => hud,
            _               => null
        };

        if (next == null)
        {
            Debug.LogWarning($"UIManager: pantalla '{screenName}' no encontrada.");
            return;
        }

        next.SetActive(true);
        currentScreen = next;
    }

    // Llamar desde cualquier script del juego para pausar
    public void OpenPause()
    {
        Time.timeScale = 0f;
        ShowScreen("Pause");
    }

    public void ClosePause()
    {
        Time.timeScale = 1f;
        ShowScreen("HUD");
    }

    public void TriggerGameOver()
    {
        Time.timeScale = 0f;
        ShowScreen("GameOver");
    }

    public void TriggerLevelComplete()
    {
        Time.timeScale = 0f;
        ShowScreen("LevelComplete");
    }
}