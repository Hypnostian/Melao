using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    // Niveles completados
    public List<string> completedLevels = new();

    // Ajustes de audio
    public float musicVolume = 0.8f;
    public float sfxVolume   = 1.0f;
    public bool  vibration   = true;
}