using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Aplicar ajustes guardados al arrancar
        ApplySavedSettings();
    }

    public void ApplySavedSettings()
    {
        SaveData data = SaveSystem.Load();
        SetMusicVolume(data.musicVolume);
        SetSFXVolume(data.sfxVolume);
    }

    public void SetMusicVolume(float value)
    {
        float db = value > 0.001f
            ? Mathf.Log10(value) * 20f
            : -80f;
        audioMixer.SetFloat("MusicVolume", db);
    }

    public void SetSFXVolume(float value)
    {
        float db = value > 0.001f
            ? Mathf.Log10(value) * 20f
            : -80f;
        audioMixer.SetFloat("SFXVolume", db);
    }
}