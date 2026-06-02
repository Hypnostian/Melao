using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    // Niveles completados
    public List<string> completedLevels = new();

    // Último nivel jugado (para continuar partida)
    public string lastLevel = "";

    // Siguiente nivel después de completar el actual (progresión)
    public string nextLevel = "";

    // Ajustes de audio
    public float musicVolume = 0.8f;
    public float sfxVolume   = 1.0f;
    public bool  vibration   = true;
}