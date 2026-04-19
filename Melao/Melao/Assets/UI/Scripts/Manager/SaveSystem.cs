using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class SaveSystem
{
    private static readonly string PATH =
        Path.Combine(Application.persistentDataPath, "melao_save.json");

    private static SaveData current;

    // — Cargar —

    public static SaveData Load()
    {
        if (current != null) return current;

        if (File.Exists(PATH))
        {
            string json = File.ReadAllText(PATH);
            current = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("Partida cargada desde: " + PATH);
        }
        else
        {
            current = new SaveData();
            Debug.Log("No se encontró partida, creando nueva.");
        }

        return current;
    }

    // — Guardar —

    public static void Save()
    {
        if (current == null) return;
        string json = JsonUtility.ToJson(current, prettyPrint: true);
        File.WriteAllText(PATH, json);
        Debug.Log("Partida guardada.");
    }

    // — Niveles completados —

    public static void MarkLevelComplete(string levelName)
    {
        SaveData data = Load();
        if (!data.completedLevels.Contains(levelName))
        {
            data.completedLevels.Add(levelName);
            Save();
        }
    }

    public static bool IsLevelComplete(string levelName)
    {
        return Load().completedLevels.Contains(levelName);
    }

    // — Ajustes —

    public static void SaveSettings(float music, float sfx, bool vib)
    {
        SaveData data = Load();
        data.musicVolume = music;
        data.sfxVolume   = sfx;
        data.vibration   = vib;
        Save();
    }

    // — Borrar partida —

    public static void DeleteSave()
    {
        current = null;
        if (File.Exists(PATH))
        {
            File.Delete(PATH);
            Debug.Log("Partida borrada.");
        }
    }
}