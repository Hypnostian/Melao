using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioSource sfxSource;

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

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.outputAudioMixerGroup = FindSFXGroup();
        sfxSource.playOnAwake = false;

        ApplySavedSettings();
    }

    private AudioMixerGroup FindSFXGroup()
    {
        if (audioMixer == null) return null;
        var groups = audioMixer.FindMatchingGroups("SFX");
        return groups.Length > 0 ? groups[0] : null;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip);
    }

    public void PlaySFXAtPoint(AudioClip clip, Vector3 position)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, sfxSource.volume);
    }

    public void ApplySavedSettings()
    {
        SaveData data = SaveSystem.Load();
        SetMusicVolume(data.musicVolume);
        SetSFXVolume(data.sfxVolume);
    }

    public void SetMusicVolume(float value)
    {
        if (audioMixer == null) return;
        float db = value > 0.001f
            ? Mathf.Log10(value) * 20f
            : -80f;
        audioMixer.SetFloat("MusicVolume", db);
    }

    public void SetSFXVolume(float value)
    {
        if (audioMixer == null) return;
        float db = value > 0.001f
            ? Mathf.Log10(value) * 20f
            : -80f;
        audioMixer.SetFloat("SFXVolume", db);
    }
}