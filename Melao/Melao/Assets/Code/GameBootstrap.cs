using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameBootstrap : MonoBehaviour
{
    [Header("Frames")]
    public int targetFrameRate = 0;
    [Range(0, 2)] public int vSyncCount = 1;

    [Header("Resolucion")]
    public bool defaultToNative = true;

    private void Awake()
    {
        Application.targetFrameRate = targetFrameRate > 0 ? targetFrameRate : -1;
        QualitySettings.vSyncCount = vSyncCount;

        if (PlayerPrefs.HasKey("ResWidth"))
        {
            int w = PlayerPrefs.GetInt("ResWidth");
            int h = PlayerPrefs.GetInt("ResHeight");
            bool full = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            var mode = (FullScreenMode)PlayerPrefs.GetInt("FullscreenMode", 1);
            Screen.SetResolution(w, h, full ? mode : FullScreenMode.Windowed);
        }
        else if (defaultToNative)
        {
            Resolution native = GetNativeResolution();
            Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow, native.refreshRateRatio);
        }
    }

    private static Resolution GetNativeResolution()
    {
        Resolution best = default;
        best.width = 1920; best.height = 1080;
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.width > best.width ||
               (r.width == best.width && r.height > best.height))
                best = r;
        }
        return best;
    }
}
