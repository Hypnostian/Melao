using UnityEngine;

public class SaveTester : MonoBehaviour
{
    private void Update()
    {
        // Presiona G para ver el contenido del archivo guardado
        if (Input.GetKeyDown(KeyCode.G))
        {
            SaveData data = SaveSystem.Load();
            Debug.Log($"Música: {data.musicVolume}");
            Debug.Log($"SFX: {data.sfxVolume}");
            Debug.Log($"Vibración: {data.vibration}");
        }
    }
}