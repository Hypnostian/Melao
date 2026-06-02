using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CutsceneController : MonoBehaviour
{
    [SerializeField] private Image displayImage;
    [SerializeField] private Sprite[] cutsceneSprites;
    [SerializeField] private string nextSceneName = "Nivel 4 Choco-Lala";

    private int currentIndex;

    private void OnEnable()
    {
        currentIndex = 0;
        ShowCurrent();
    }

    private void Update()
    {
        if (cutsceneSprites == null || cutsceneSprites.Length == 0) return;

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            Advance();

        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.buttonEast.wasPressedThisFrame ||
                Gamepad.current.buttonWest.wasPressedThisFrame ||
                Gamepad.current.buttonNorth.wasPressedThisFrame ||
                Gamepad.current.startButton.wasPressedThisFrame)
                Advance();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Advance();
    }

    private void Advance()
    {
        currentIndex++;
        if (currentIndex >= cutsceneSprites.Length)
        {
            SceneLoader.Instance.LoadScene(nextSceneName);
            return;
        }
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (displayImage != null && currentIndex < cutsceneSprites.Length)
            displayImage.sprite = cutsceneSprites[currentIndex];
    }
}
