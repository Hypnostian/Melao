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
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.SetMusicVolume(value);
    }

    public void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.SetSFXVolume(value);
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
        SaveData data = SaveSystem.Load();

        // Desuscribir eventos antes de asignar valores
        musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);

        // Asignar valores sin disparar eventos
        musicSlider.value = data.musicVolume;
        sfxSlider.value   = data.sfxVolume;
        vibrationToggle.isOn = data.vibration;

        // Volver a suscribir eventos
        musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
    }

    // — Botón volver —

    public void OnBackPressed()
    {
        //TODO: repuesto en caso de que no funcione
        /*PlayerPrefs.Save(); // guardar al salir
        UIManager.Instance.ShowScreen("MainMenu");
        // Si venía de pausa, el PauseScreen puede sobreescribir esto
        // cuando implementes un sistema de pantalla anterior (back stack)*/
        SaveSystem.SaveSettings(
            musicSlider.value,
            sfxSlider.value,
            vibrationToggle.isOn
        );
        UIManager.Instance.ShowScreen("MainMenu");
    }
}