using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private GameObject mainMenuScreen;
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private GameObject settingsScreen;
    [SerializeField] private GameObject gameOverScreen;
    [SerializeField] private GameObject levelCompleteScreen;
    [SerializeField] private GameObject creditsScreen;
    [SerializeField] private GameObject cutsceneScreen;
    [SerializeField] private GameObject hud;

    private GameObject currentScreen;

    private void Awake()
    {
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

    private void HideAllScreens()
    {
        mainMenuScreen.SetActive(false);
        pauseScreen.SetActive(false);
        settingsScreen.SetActive(false);
        gameOverScreen.SetActive(false);
        levelCompleteScreen.SetActive(false);
        creditsScreen.SetActive(false);
        cutsceneScreen.SetActive(false);
        hud.SetActive(false);
    }

    private void Start()
    {
        ShowScreen("MainMenu");
    }

    private static readonly string[] PauseScreens = { "Pause", "Settings", "GameOver", "LevelComplete" };

    public void ShowScreen(string screenName)
    {
        string prevName = currentScreen != null ? GetScreenName(currentScreen) : null;
        bool prevWasPause = prevName != null && IsPauseScreen(prevName);
        bool nextIsPause = IsPauseScreen(screenName);

        if (currentScreen != null)
            currentScreen.SetActive(false);

        GameObject next = screenName switch
        {
            "MainMenu"      => mainMenuScreen,
            "Pause"         => pauseScreen,
            "Settings"      => settingsScreen,
            "GameOver"      => gameOverScreen,
            "LevelComplete" => levelCompleteScreen,
            "Credits"       => creditsScreen,
            "Cutscene"      => cutsceneScreen,
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

        if (nextIsPause && !prevWasPause)
            AudioManager.Instance?.EnterPause();
        else if (!nextIsPause && prevWasPause)
            AudioManager.Instance?.ExitPause();
    }

    private string GetScreenName(GameObject screen)
    {
        if (screen == mainMenuScreen) return "MainMenu";
        if (screen == pauseScreen) return "Pause";
        if (screen == settingsScreen) return "Settings";
        if (screen == gameOverScreen) return "GameOver";
        if (screen == levelCompleteScreen) return "LevelComplete";
        if (screen == creditsScreen) return "Credits";
        if (screen == cutsceneScreen) return "Cutscene";
        if (screen == hud) return "HUD";
        return null;
    }

    private bool IsPauseScreen(string name)
    {
        foreach (var s in PauseScreens)
            if (s == name) return true;
        return false;
    }

    public bool IsInGame => currentScreen == hud;
    public bool IsPaused => currentScreen == pauseScreen;

    public void OpenPause()
    {
        if (!IsInGame) return;
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
