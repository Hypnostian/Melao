using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class SettingsScreen : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Control")]
    [SerializeField] private Toggle vibrationToggle;

    [Header("Navegación")]
    [SerializeField] private GameObject panelAudio;
    [SerializeField] private GameObject panelControl;

    [Header("Rebinding")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private GameObject bindingRowPrefab;

    private List<GameObject> bindingRows = new List<GameObject>();
    private string returnScreen = "MainMenu";

    private void OnEnable()
    {
        AutoDiscoverAsset();
        LoadSettings();
        ShowPanel("Audio");
    }

    private void AutoDiscoverAsset()
    {
        if (inputActions != null) return;
        if (ControlRebinder.Instance != null)
            inputActions = ControlRebinder.Instance.ActionsAsset;
        if (inputActions == null)
            inputActions = FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Exclude)?.actions;
        if (inputActions == null)
        {
            var pc = new PlayerControls();
            inputActions = pc.asset;
        }
    }

    public void SetReturnScreen(string screen)
    {
        returnScreen = screen;
    }

    public void OnAudioTabPressed()  => ShowPanel("Audio");
    public void OnControlTabPressed() => ShowPanel("Control");

    private void ShowPanel(string panel)
    {
        if (panelAudio != null) panelAudio.SetActive(panel == "Audio");
        if (panelControl != null) panelControl.SetActive(panel == "Control");

        if (panel == "Control")
        {
        }
    }

    private void RebuildBindingUI()
    {
        foreach (var row in bindingRows)
            Destroy(row);
        bindingRows.Clear();

        if (panelControl == null || inputActions == null) return;

        var vlg = panelControl.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = panelControl.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.spacing = 6;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
        }

        var csf = panelControl.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = panelControl.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        foreach (var map in inputActions.actionMaps)
        {
            foreach (var action in map.actions)
            {
                string gamepadDisplay = null;
                int gamepadIndex = -1;
                var keyboardBindings = new List<(int index, string display, string partName)>();

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite) continue;
                    if (b.isPartOfComposite && string.IsNullOrEmpty(b.name)) continue;

                    string display = action.GetBindingDisplayString(i, out _, out _);
                    if (string.IsNullOrEmpty(display)) display = "Sin asignar";

                    if (b.path.Contains("<Gamepad>"))
                    {
                        gamepadDisplay = display;
                        gamepadIndex = i;
                    }
                    else
                    {
                        keyboardBindings.Add((i, display, b.name));
                    }
                }

                if (keyboardBindings.Count == 0 && gamepadIndex < 0)
                {
                    CreateBindingRow(action.name, null, null, action, -1, -1);
                }
                else if (keyboardBindings.Count == 0)
                {
                    CreateBindingRow(action.name, null, gamepadDisplay, action, -1, gamepadIndex);
                }
                else
                {
                    foreach (var (idx, kbDisplay, partName) in keyboardBindings)
                    {
                        string suffix = partName?.ToLower() switch
                        {
                            "negative" => "izquierda",
                            "positive" => "derecha",
                            _ => partName
                        };
                        string label = string.IsNullOrEmpty(suffix) ? action.name : $"{action.name} ({suffix})";
                        CreateBindingRow(label, kbDisplay, gamepadDisplay, action, idx, gamepadIndex);
                    }
                }
            }
        }
    }

    private void CreateBindingRow(string label, string keyboardDisplay, string gamepadDisplay, InputAction action, int keyboardIndex, int gamepadIndex)
    {
        Transform parent = panelControl.transform;

        GameObject row;
        if (bindingRowPrefab != null)
        {
            row = Instantiate(bindingRowPrefab, parent);
            SetupRowChildren(row, label, keyboardDisplay, gamepadDisplay, action, keyboardIndex, gamepadIndex);
        }
        else
        {
            row = new GameObject("BindingRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.spacing = 12;
            hlg.padding = new RectOffset(8, 8, 4, 4);

            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 32;

            var csf = row.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text labelText = CreateText("Label", row.transform, label, FontStyle.Bold, 16);
            labelText.alignment = TextAnchor.MiddleLeft;
            var labelLayout = labelText.GetComponent<LayoutElement>();
            if (labelLayout == null) labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = 160;

            Text gpText = CreateText("GamepadBinding", row.transform, gamepadDisplay ?? "—", FontStyle.Normal, 16);
            gpText.alignment = TextAnchor.MiddleLeft;
            var gpLayout = gpText.GetComponent<LayoutElement>();
            if (gpLayout == null) gpLayout = gpText.gameObject.AddComponent<LayoutElement>();
            gpLayout.minWidth = 140;

            Text kbText = CreateText("KeyboardBinding", row.transform, keyboardDisplay ?? "—", FontStyle.Normal, 16);
            kbText.alignment = TextAnchor.MiddleLeft;
            var kbLayout = kbText.GetComponent<LayoutElement>();
            if (kbLayout == null) kbLayout = kbText.gameObject.AddComponent<LayoutElement>();
            kbLayout.minWidth = 140;

            if (gamepadIndex >= 0)
                MakeClickable(gpText, action, gamepadIndex);
            if (keyboardIndex >= 0)
                MakeClickable(kbText, action, keyboardIndex);
        }

        bindingRows.Add(row);
    }

    private void SetupRowChildren(GameObject row, string label, string keyboardDisplay, string gamepadDisplay, InputAction action, int keyboardIndex, int gamepadIndex)
    {
        var labelTxt = row.transform.Find("Label")?.GetComponent<Text>();
        if (labelTxt != null)
        {
            labelTxt.text = label;
            labelTxt.alignment = TextAnchor.MiddleLeft;
        }

        var gpTxt = row.transform.Find("GamepadBinding")?.GetComponent<Text>();
        if (gpTxt != null)
        {
            gpTxt.text = gamepadDisplay ?? "—";
            gpTxt.alignment = TextAnchor.MiddleLeft;
            if (gamepadIndex >= 0)
                MakeClickable(gpTxt, action, gamepadIndex);
        }

        var kbTxt = row.transform.Find("KeyboardBinding")?.GetComponent<Text>();
        if (kbTxt != null)
        {
            kbTxt.text = keyboardDisplay ?? "—";
            kbTxt.alignment = TextAnchor.MiddleLeft;
            if (keyboardIndex >= 0)
                MakeClickable(kbTxt, action, keyboardIndex);
        }

        var resetBtn = row.transform.Find("ResetBtn")?.GetComponent<Button>();
        if (resetBtn != null)
        {
            resetBtn.onClick.RemoveAllListeners();
            resetBtn.onClick.AddListener(() =>
            {
                action?.RemoveAllBindingOverrides();
                SaveOverrides();
                RebuildBindingUI();
            });
        }
    }

    private void MakeClickable(Text text, InputAction action, int bindingIndex)
    {
        var wrapper = new GameObject("Clickable", typeof(RectTransform));
        wrapper.transform.SetParent(text.transform.parent, false);
        wrapper.transform.SetSiblingIndex(text.transform.GetSiblingIndex());

        text.transform.SetParent(wrapper.transform, false);
        text.raycastTarget = false; // Let the wrapper's Image handle clicks instead

        var img = wrapper.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;

        var btn = wrapper.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        // Always give the wrapper a LayoutElement with the text's minWidth (or default)
        var srcLayout = text.GetComponent<LayoutElement>();
        var wLayout = wrapper.AddComponent<LayoutElement>();
        wLayout.minWidth = srcLayout != null ? srcLayout.minWidth : 100;
        wLayout.preferredWidth = srcLayout != null ? srcLayout.preferredWidth : -1;
        wLayout.flexibleWidth = srcLayout != null ? srcLayout.flexibleWidth : 0;

        var wFitter = wrapper.AddComponent<ContentSizeFitter>();
        wFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        wFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        int captured = bindingIndex;
        btn.onClick.AddListener(() => StartRebindingFor(action, captured, text.transform));
    }

    private Text CreateText(string name, Transform parent, string text, FontStyle style, int fontSize = 14)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = Color.white;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return txt;
    }

    private Button CreateButton(string name, Transform parent, string btnText, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        var txt = label.AddComponent<Text>();
        txt.text = btnText;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 16;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        var fitter = label.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 32);

        return btn;
    }

    private InputAction pendingRebindAction;
    private int pendingRebindIndex;
    private bool pendingRebindTriggered;

    private void Update()
    {
        if (pendingRebindAction == null || pendingRebindTriggered) return;
        if (TryCaptureKeyboardInput()) return;
    }

    private bool TryCaptureKeyboardInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        foreach (var key in keyboard.allKeys)
        {
            if (!key.wasPressedThisFrame) continue;
            pendingRebindTriggered = true;
            if (key.keyCode == Key.Escape)
            {
                CompleteRebind(pendingRebindAction, pendingRebindIndex, null);
                return true;
            }
            CompleteRebind(pendingRebindAction, pendingRebindIndex, key.path);
            return true;
        }
        return false;
    }

    private void OnDisable()
    {
        if (pendingRebindAction != null)
        {
            try { pendingRebindAction.Enable(); } catch { }
            pendingRebindAction = null;
        }
    }

    private void StartRebindingFor(InputAction action, int bindingIndex, Transform textTransform)
    {
        if (pendingRebindAction != null)
        {
            try { pendingRebindAction.Enable(); } catch { }
            pendingRebindAction = null;
        }

        var textComp = textTransform?.GetComponent<Text>();
        if (textComp != null)
            textComp.text = "Presiona una tecla...";

        action.Disable();
        pendingRebindAction = action;
        pendingRebindIndex = bindingIndex;
        pendingRebindTriggered = false;

        // Also poll with Input System as fallback
        StartCoroutine(GamepadPoller(action, bindingIndex));
    }

    private void OnGUI()
    {
        if (pendingRebindAction == null || pendingRebindTriggered) return;
        if (Event.current.type != EventType.KeyDown) return;

        var keyCode = Event.current.keyCode;
        if (keyCode == KeyCode.None) return;

        pendingRebindTriggered = true;

        if (keyCode == KeyCode.Escape)
        {
            CompleteRebind(pendingRebindAction, pendingRebindIndex, null);
            return;
        }

        string path = string.Format("<Keyboard>/{0}", KeyCodeToInputSystemName(keyCode));
        CompleteRebind(pendingRebindAction, pendingRebindIndex, path);
    }

    private string KeyCodeToInputSystemName(KeyCode keyCode)
    {
        string name = keyCode.ToString();

        // Numpad
        if (name.StartsWith("Keypad"))
            return "numpad" + name.Substring(6).ToLower(); // Keypad0 → numpad0

        // Alpha numbers
        if (name.StartsWith("Alpha"))
            return name.Substring(5).ToLower(); // Alpha1 → 1

        // Modifier & special keys
        switch (name)
        {
            case "LeftControl":  return "leftCtrl";
            case "RightControl": return "rightCtrl";
            case "LeftAlt":      return "leftAlt";
            case "RightAlt":     return "rightAlt";
            case "LeftShift":    return "leftShift";
            case "RightShift":   return "rightShift";
            case "LeftCommand":  return "leftCommand";
            case "RightCommand": return "rightCommand";
            case "LeftWindows":  return "leftWindows";
            case "RightWindows": return "rightWindows";
            case "CapsLock":     return "capsLock";
            case "Numlock":      return "numLock";
            case "ScrollLock":   return "scrollLock";
            case "BackQuote":    return "backquote";
            case "Minus":        return "minus";
            case "Equals":       return "equals";
            case "LeftBracket":  return "leftBracket";
            case "RightBracket": return "rightBracket";
            case "Backslash":    return "backslash";
            case "Semicolon":    return "semicolon";
            case "Quote":        return "quote";
            case "Period":       return "period";
            case "Comma":        return "comma";
            case "Slash":        return "slash";
            case "PageDown":     return "pageDown";
            case "PageUp":       return "pageUp";
            case "Delete":       return "delete";
            case "Insert":       return "insert";
            case "Print":        return "printScreen";
            case "SysReq":       return "sysRq";
            case "Break":        return "pause";
            case "DoubleQuote":  return "quote";
            case "Return":       return "enter";
            case "KeypadEnter":  return "numpadEnter";
            case "UpArrow":      return "upArrow";
            case "DownArrow":    return "downArrow";
            case "LeftArrow":    return "leftArrow";
            case "RightArrow":   return "rightArrow";
        }

        // Everything else: lowercase (W → w, Space → space, etc.)
        return name.ToLower();
    }

    private System.Collections.IEnumerator GamepadPoller(InputAction action, int bindingIndex)
    {
        yield return null;

        while (pendingRebindAction == action && !pendingRebindTriggered)
        {
            var gp = Gamepad.current;
            if (gp != null)
            {
                string found = null;
                if (gp.buttonSouth.wasPressedThisFrame) found = gp.buttonSouth.path;
                else if (gp.buttonEast.wasPressedThisFrame) found = gp.buttonEast.path;
                else if (gp.buttonWest.wasPressedThisFrame) found = gp.buttonWest.path;
                else if (gp.buttonNorth.wasPressedThisFrame) found = gp.buttonNorth.path;
                else if (gp.leftShoulder.wasPressedThisFrame) found = gp.leftShoulder.path;
                else if (gp.rightShoulder.wasPressedThisFrame) found = gp.rightShoulder.path;
                else if (gp.leftTrigger.wasPressedThisFrame) found = gp.leftTrigger.path;
                else if (gp.rightTrigger.wasPressedThisFrame) found = gp.rightTrigger.path;
                else if (gp.selectButton.wasPressedThisFrame) found = gp.selectButton.path;
                else if (gp.startButton.wasPressedThisFrame) found = gp.startButton.path;
                else if (gp.leftStickButton.wasPressedThisFrame) found = gp.leftStickButton.path;
                else if (gp.rightStickButton.wasPressedThisFrame) found = gp.rightStickButton.path;
                else if (gp.dpad.up.wasPressedThisFrame) found = gp.dpad.up.path;
                else if (gp.dpad.down.wasPressedThisFrame) found = gp.dpad.down.path;
                else if (gp.dpad.left.wasPressedThisFrame) found = gp.dpad.left.path;
                else if (gp.dpad.right.wasPressedThisFrame) found = gp.dpad.right.path;

                if (found != null)
                {
                    pendingRebindTriggered = true;
                    CompleteRebind(action, bindingIndex, found);
                    yield break;
                }
            }
            yield return null;
        }
    }

    private void CompleteRebind(InputAction action, int bindingIndex, string newPath)
    {
        if (pendingRebindAction == action)
        {
            pendingRebindAction = null;
            pendingRebindTriggered = true;
        }

        if (newPath == null)
        {
            try { action.Enable(); } catch { }
            if (this != null) RebuildBindingUI();
            return;
        }

        action.ApplyBindingOverride(bindingIndex, newPath);
        try { action.Enable(); } catch { }
        SaveOverrides();
        if (this != null) RebuildBindingUI();
    }

    // — Audio —

    public void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);
    }

    public void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(value);
    }

    // — Control —

    public void OnVibrationToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt("vibration", isOn ? 1 : 0);
    }

    // — Persistencia —

    private void LoadSettings()
    {
        SaveData data = SaveSystem.Load();

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicSlider.value = data.musicVolume;
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            sfxSlider.value = data.sfxVolume;
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        if (vibrationToggle != null)
            vibrationToggle.isOn = data.vibration;

        if (inputActions != null)
        {
            string json = PlayerPrefs.GetString("ControlRebinds", "");
            if (!string.IsNullOrEmpty(json))
                inputActions.LoadBindingOverridesFromJson(json);
        }
    }

    private void SaveOverrides()
    {
        if (inputActions == null) return;
        string json = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("ControlRebinds", json);
        PlayerPrefs.Save();
    }

    // — Botón volver —

    public void OnBackPressed()
    {
        SaveSystem.SaveSettings(
            musicSlider != null ? musicSlider.value : 0.8f,
            sfxSlider != null ? sfxSlider.value : 1f,
            vibrationToggle != null && vibrationToggle.isOn
        );

        // RebindingButton ya guardó en PlayerPrefs al reasignar cada tecla.
        // Solo sincronizamos al jugador para que las use de inmediato.
        var player = FindFirstObjectByType<PlayerController2_5D>(FindObjectsInactive.Exclude);
        if (player != null) player.ReloadOverrides();

        UIManager.Instance.ShowScreen(returnScreen);
    }
}
