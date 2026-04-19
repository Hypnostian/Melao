using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsScreen : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Control")]
    [SerializeField] private Toggle vibrationToggle;

    [Header("Navegación")]
    [SerializeField] private GameObject panelAudio;
    [SerializeField] private GameObject panelControl;

    // Claves para PlayerPrefs (persistencia entre sesiones)
    private const string KEY_MUSIC = "vol_music";
    private const string KEY_SFX   = "vol_sfx";
    private const string KEY_VIB   = "vibration";

    private void OnEnable()
    {
        // Cargar valores guardados cada vez que se abre la pantalla
        LoadSettings();
        // Mostrar panel de audio por defecto al abrir
        ShowPanel("Audio");
    }

    // — Navegación entre paneles —

    public void OnAudioTabPressed()  => ShowPanel("Audio");
    public void OnControlTabPressed() => ShowPanel("Control");

    private void ShowPanel(string panel)
    {
        panelAudio.SetActive(panel == "Audio");
        panelControl.SetActive(panel == "Control");
    }

    // — Audio —

    public void OnMusicVolumeChanged(float value)
    {
        // AudioMixer usa decibeles: convertir de 0-1 a -80/0 dB
        float db = value > 0.001f
            ? Mathf.Log10(value) * 20f
            : -80f;
        audioMixer.SetFloat("MusicVolume", db);
        PlayerPrefs.SetFloat(KEY_MUSIC, value);
    }

    public void OnSFXVolumeChanged(float value)
    {
        float db = value > 0.001f
            ? Mathf.Log10(value) * 20f
            : -80f;
        audioMixer.SetFloat("SFXVolume", db);
        PlayerPrefs.SetFloat(KEY_SFX, value);
    }

    // — Control —

    public void OnVibrationToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt(KEY_VIB, isOn ? 1 : 0);
        // La vibración se consulta desde el PlayerController con:
        // PlayerPrefs.GetInt("vibration", 1) == 1
    }

    // — Persistencia —

    private void LoadSettings()
    {
        float music = PlayerPrefs.GetFloat(KEY_MUSIC, 0.8f);
        float sfx   = PlayerPrefs.GetFloat(KEY_SFX,   1.0f);
        bool  vib   = PlayerPrefs.GetInt(KEY_VIB,   1) == 1;

        musicSlider.value     = music;
        sfxSlider.value       = sfx;
        vibrationToggle.isOn  = vib;

        // Aplicar al mixer también al cargar
        OnMusicVolumeChanged(music);
        OnSFXVolumeChanged(sfx);
    }

    // — Botón volver —

    public void OnBackPressed()
    {
        PlayerPrefs.Save(); // guardar al salir
        UIManager.Instance.ShowScreen("MainMenu");
        // Si venía de pausa, el PauseScreen puede sobreescribir esto
        // cuando implementes un sistema de pantalla anterior (back stack)
    }
}