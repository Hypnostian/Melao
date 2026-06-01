using UnityEngine;
using UnityEngine.InputSystem;

public class ControlRebinder : MonoBehaviour
{
    public static ControlRebinder Instance { get; private set; }

    public InputActionAsset ActionsAsset => actionsAsset;

    [SerializeField] private InputActionAsset actionsAsset;

    private const string PREFS_KEY = "ControlRebinds";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (actionsAsset == null)
        {
            var pi = FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Exclude);
            if (pi != null)
                actionsAsset = pi.actions;
        }
        if (actionsAsset == null)
        {
            var pc = new PlayerControls();
            actionsAsset = pc.asset;
        }
        if (actionsAsset != null)
        {
            EnsureGamepadBindings(actionsAsset);
            LoadOverrides(actionsAsset);
        }
    }

    public void LoadOverrides(InputActionAsset asset)
    {
        if (asset == null) return;
        string json = PlayerPrefs.GetString(PREFS_KEY, "");
        if (!string.IsNullOrEmpty(json))
            asset.LoadBindingOverridesFromJson(json);
    }

    public void ApplySavedOverrides()
    {
        var pi = FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Exclude);
        if (pi != null && pi.actions != null)
        {
            EnsureGamepadBindings(pi.actions);
            LoadOverrides(pi.actions);
        }
    }

    public void SaveOverrides(InputActionAsset asset)
    {
        if (asset == null) return;
        string json = asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(PREFS_KEY, json);
        PlayerPrefs.Save();
    }

    public void ResetAllOverrides(InputActionAsset asset)
    {
        if (asset == null) return;
        foreach (var map in asset.actionMaps)
        {
            foreach (var action in map.actions)
                action.RemoveAllBindingOverrides();
        }
        PlayerPrefs.DeleteKey(PREFS_KEY);
        PlayerPrefs.Save();
    }

    public void ResetActionOverrides(InputAction action)
    {
        if (action == null) return;
        action.RemoveAllBindingOverrides();
    }

    public void EnsureGamepadBindings(InputActionAsset asset)
    {
        var map = asset.FindActionMap("Player");
        if (map == null) return;

        AddIfMissing(map.FindAction("Move"), "<Gamepad>/leftStick/x");
        AddIfMissing(map.FindAction("Jump"), "<Gamepad>/buttonSouth");
        AddIfMissing(map.FindAction("UsePowerUp"), "<Gamepad>/buttonEast");
        AddIfMissing(map.FindAction("NextPowerUp"), "<Gamepad>/dpadDown");
        AddIfMissing(map.FindAction("PrevPowerUp"), "<Gamepad>/dpadUp");
    }

    private void AddIfMissing(InputAction action, string path)
    {
        if (action == null) return;
        foreach (var b in action.bindings)
        {
            if (!string.IsNullOrEmpty(b.path) && b.path.Contains("Gamepad"))
                return;
        }
        action.AddBinding(path);
    }
}
