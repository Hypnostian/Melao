using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

    [Header("Resolucion base")]
    public int referenceWidth = 1920;
    public int referenceHeight = 1080;

    [Header("Pantalla completa")]
    public bool startFullscreen = true;
    public FullScreenMode fullscreenMode = FullScreenMode.FullScreenWindow;

    [Header("Letterboxing")]
    public Color barsColor = Color.black;
    public bool enableLetterboxing = true;

    [Header("Teclas rapidas")]
    public KeyCode toggleFullscreenKey = KeyCode.F11;

    private Camera mainCam;
    private float targetAspect;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private Camera barsCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        mainCam = Camera.main;
        targetAspect = (float)referenceWidth / referenceHeight;

        if (startFullscreen)
            ApplyNativeFullscreen();
        else
            ApplyCurrentResolution();
    }

    private void Start()
    {
        ConfigureAllCanvases();
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            OnResolutionChanged();
        }

        if (Input.GetKeyDown(toggleFullscreenKey)) ToggleFullscreen();
    }

    public void ApplyNativeFullscreen()
    {
        Resolution native = GetNativeResolution();
        Screen.SetResolution(native.width, native.height, fullscreenMode, native.refreshRateRatio);
        StartCoroutine(DelayedUIUpdate());
    }

    public void ToggleFullscreen()
    {
        if (Screen.fullScreen)
            Screen.SetResolution(referenceWidth, referenceHeight, FullScreenMode.Windowed);
        else
            ApplyNativeFullscreen();
        StartCoroutine(DelayedUIUpdate());
    }

    public void SetResolution(int width, int height, bool fullscreen = true)
    {
        FullScreenMode mode = fullscreen ? fullscreenMode : FullScreenMode.Windowed;
        Screen.SetResolution(width, height, mode);
        StartCoroutine(DelayedUIUpdate());
    }

    public Resolution[] GetAvailableResolutions() => Screen.resolutions;

    private Resolution GetNativeResolution()
    {
        Resolution best = default;
        best.width = 1920; best.height = 1080;
        Resolution[] resolutions = Screen.resolutions;
        if (resolutions == null || resolutions.Length == 0) return best;

        foreach (Resolution r in resolutions)
        {
            if (r.width > best.width ||
               (r.width == best.width && r.height > best.height) ||
               (r.width == best.width && r.height == best.height &&
                r.refreshRateRatio.value > best.refreshRateRatio.value))
                best = r;
        }
        return best;
    }

    private void ApplyCurrentResolution()
    {
        if (!Screen.fullScreen) return;
        Screen.SetResolution(Screen.width, Screen.height, fullscreenMode);
    }

    private void OnResolutionChanged()
    {
        ApplyLetterboxing();
        ConfigureAllCanvases();
    }

    private void ApplyLetterboxing()
    {
        if (!enableLetterboxing || mainCam == null) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float scaleHeight = screenAspect / targetAspect;

        if (Mathf.Approximately(scaleHeight, 1f))
            mainCam.rect = new Rect(0f, 0f, 1f, 1f);
        else if (scaleHeight < 1f)
        {
            float y = (1f - scaleHeight) / 2f;
            mainCam.rect = new Rect(0f, y, 1f, scaleHeight);
        }
        else
        {
            float scaleWidth = 1f / scaleHeight;
            float x = (1f - scaleWidth) / 2f;
            mainCam.rect = new Rect(x, 0f, scaleWidth, 1f);
        }

        Camera barsCam = GetOrCreateBarsCamera();
        barsCam.backgroundColor = barsColor;
    }

    private void ConfigureAllCanvases()
    {
        var allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in allCanvases)
        {
            if (canvas == null) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(referenceWidth, referenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private Camera GetOrCreateBarsCamera()
    {
        if (barsCamera != null) return barsCamera;
        var go = new GameObject("_BarsCamera");
        DontDestroyOnLoad(go);
        barsCamera = go.AddComponent<Camera>();
        barsCamera.clearFlags = CameraClearFlags.SolidColor;
        barsCamera.backgroundColor = barsColor;
        barsCamera.cullingMask = 0;
        barsCamera.depth = mainCam ? mainCam.depth - 1 : -2;
        barsCamera.rect = new Rect(0, 0, 1, 1);
        return barsCamera;
    }

    private IEnumerator DelayedUIUpdate()
    {
        yield return null;
        ApplyLetterboxing();
        ConfigureAllCanvases();
    }

    private void OnValidate()
    {
        if (mainCam == null) mainCam = Camera.main;
    }
}
