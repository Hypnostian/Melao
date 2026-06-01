using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class RebindingButton : MonoBehaviour
{
    [Header("Asignar en el Inspector")]
    [Tooltip("Arrastra el archivo PlayerControls.inputactions (Assets/Code/)")]
    [SerializeField] private InputActionAsset actions;

    [Tooltip("Nombre exacto: Move, Jump, UsePowerUp, NextPowerUp, PrevPowerUp")]
    [SerializeField] private string actionName;

    [Tooltip("Move: 1=izquierda, 2=derecha, 3=gamepad\n" +
             "Jump: 0=teclado, 1=gamepad\n" +
             "UsePowerUp/NextPowerUp/PrevPowerUp: 0=teclado, 1=gamepad")]
    [SerializeField] private int bindingIndex;

    private InputAction action;

    private bool initialized;

    private void Start()
    {
        var btn = GetComponent<Button>();
        if (btn == null) return;

        // Evita que el Text intercepte los clicks
        var legacyText = GetComponentInChildren<Text>(true);
        if (legacyText != null) legacyText.raycastTarget = false;

        ResolveAction();
        UpdateDisplay();
        btn.onClick.AddListener(OnClick);
        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized) return;
        ResolveAction();
        UpdateDisplay();
    }

    private void ResolveAction()
    {
        if (actions == null) return;
        action = actions.FindAction(actionName);
    }

    private string GetBindingDisplay()
    {
        if (action == null) return "—";
        try { return action.GetBindingDisplayString(bindingIndex, out _, out _); }
        catch { return "—"; }
    }

    public void UpdateDisplay()
    {
        // Carga overrides guardados antes de mostrar el texto
        if (actions != null)
        {
            string json = PlayerPrefs.GetString("ControlRebinds", "");
            if (!string.IsNullOrEmpty(json))
                actions.LoadBindingOverridesFromJson(json);
        }

        var childText = GetComponentInChildren<Text>(true);
        if (childText != null) { childText.text = GetBindingDisplay(); return; }

        // También busca TextMeshPro en los hijos (Unity 6000 crea estos por defecto)
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            var tmp = t.GetComponent("TextMeshProUGUI") as UnityEngine.Component;
            if (tmp == null) continue;
            var prop = tmp.GetType().GetProperty("text");
            if (prop != null) { prop.SetValue(tmp, GetBindingDisplay()); return; }
        }
    }

    private void SetText(string value)
    {
        var childText = GetComponentInChildren<Text>(true);
        if (childText != null) { childText.text = value; return; }

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            var tmp = t.GetComponent("TextMeshProUGUI") as UnityEngine.Component;
            if (tmp == null) continue;
            var prop = tmp.GetType().GetProperty("text");
            if (prop != null) { prop.SetValue(tmp, value); return; }
        }
    }

    private void OnClick()
    {
        if (action == null) return;
        StartCoroutine(RebindCoroutine());
    }

    private System.Collections.IEnumerator RebindCoroutine()
    {
        action.Disable();
        SetText("Presiona una tecla...");

        yield return null;

        while (true)
        {
            if (TryCaptureKeyboard()) yield break;
            if (TryCaptureGamepad()) yield break;
            yield return null;
        }
    }

    private bool TryCaptureKeyboard()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        foreach (var key in keyboard.allKeys)
        {
            if (!key.wasPressedThisFrame) continue;
            if (key.keyCode == Key.Escape) { CancelRebind(); return true; }
            ApplyOverride(key.path);
            return true;
        }
        return false;
    }

    private bool TryCaptureGamepad()
    {
        var gp = Gamepad.current;
        if (gp == null) return false;

        if (gp.buttonSouth.wasPressedThisFrame) { ApplyOverride(gp.buttonSouth.path); return true; }
        if (gp.buttonEast.wasPressedThisFrame) { ApplyOverride(gp.buttonEast.path); return true; }
        if (gp.buttonWest.wasPressedThisFrame) { ApplyOverride(gp.buttonWest.path); return true; }
        if (gp.buttonNorth.wasPressedThisFrame) { ApplyOverride(gp.buttonNorth.path); return true; }
        if (gp.leftShoulder.wasPressedThisFrame) { ApplyOverride(gp.leftShoulder.path); return true; }
        if (gp.rightShoulder.wasPressedThisFrame) { ApplyOverride(gp.rightShoulder.path); return true; }
        if (gp.leftTrigger.wasPressedThisFrame) { ApplyOverride(gp.leftTrigger.path); return true; }
        if (gp.rightTrigger.wasPressedThisFrame) { ApplyOverride(gp.rightTrigger.path); return true; }
        if (gp.selectButton.wasPressedThisFrame) { ApplyOverride(gp.selectButton.path); return true; }
        if (gp.startButton.wasPressedThisFrame) { ApplyOverride(gp.startButton.path); return true; }
        if (gp.leftStickButton.wasPressedThisFrame) { ApplyOverride(gp.leftStickButton.path); return true; }
        if (gp.rightStickButton.wasPressedThisFrame) { ApplyOverride(gp.rightStickButton.path); return true; }
        if (gp.dpad.up.wasPressedThisFrame) { ApplyOverride(gp.dpad.up.path); return true; }
        if (gp.dpad.down.wasPressedThisFrame) { ApplyOverride(gp.dpad.down.path); return true; }
        if (gp.dpad.left.wasPressedThisFrame) { ApplyOverride(gp.dpad.left.path); return true; }
        if (gp.dpad.right.wasPressedThisFrame) { ApplyOverride(gp.dpad.right.path); return true; }

        return false;
    }

    private void ApplyOverride(string path)
    {
        action.ApplyBindingOverride(bindingIndex, path);
        action.Enable();
        UpdateDisplay();
        SaveGlobalOverrides();
    }

    private void CancelRebind()
    {
        action.Enable();
        UpdateDisplay();
    }

    private void SaveGlobalOverrides()
    {
        if (actions == null) return;
        string json = actions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("ControlRebinds", json);
        PlayerPrefs.Save();

        // Sincroniza con el jugador en tiempo real
        var player = FindFirstObjectByType<PlayerController2_5D>(FindObjectsInactive.Exclude);
        if (player != null) player.ReloadOverrides();
    }
}
