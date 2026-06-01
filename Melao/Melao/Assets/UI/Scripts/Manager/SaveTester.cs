using UnityEngine;
using UnityEngine.InputSystem;

public class SaveTester : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            SaveData data = SaveSystem.Load();
            Debug.Log($"Música: {data.musicVolume}");
            Debug.Log($"SFX: {data.sfxVolume}");
            Debug.Log($"Vibración: {data.vibration}");
        }
    }
}