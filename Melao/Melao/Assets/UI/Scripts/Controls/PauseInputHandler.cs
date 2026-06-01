using UnityEngine;
using UnityEngine.InputSystem;

public class PauseInputHandler : MonoBehaviour
{
    private InputAction pauseAction;

    private void Awake()
    {
        pauseAction = new InputAction("Pause", InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.performed += OnPausePerformed;
    }

    private void OnEnable()
    {
        pauseAction?.Enable();
    }

    private void OnDisable()
    {
        pauseAction?.Disable();
    }

    private void OnDestroy()
    {
        pauseAction?.Dispose();
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (UIManager.Instance == null) return;

        if (UIManager.Instance.IsPaused)
            UIManager.Instance.ClosePause();
        else
            UIManager.Instance.OpenPause();
    }
}
