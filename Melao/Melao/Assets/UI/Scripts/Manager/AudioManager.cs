using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Pause ducking")]
    [SerializeField] [Range(0f, 1f)] private float duckVolume = 0.15f;

    private AudioClip currentMusic;
    private float preDuckVolume = 0.8f;
    private bool isDucked;
    private bool sfxPaused;

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

        if (audioMixer == null)
        {
            var allMixers = Resources.FindObjectsOfTypeAll<AudioMixer>();
            foreach (var m in allMixers)
            {
                if (m.name == "MelaoMixer") { audioMixer = m; break; }
            }
            if (audioMixer == null && allMixers.Length > 0) audioMixer = allMixers[0];
        }

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.outputAudioMixerGroup = FindGroup("SFX");
        sfxSource.playOnAwake = false;

        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.outputAudioMixerGroup = FindGroup("Music");
        musicSource.playOnAwake = false;
        musicSource.loop = true;

        ApplySavedSettings();
    }

    private AudioMixerGroup FindGroup(string name)
    {
        if (audioMixer == null) return null;
        var groups = audioMixer.FindMatchingGroups(name);
        return groups.Length > 0 ? groups[0] : null;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null || sfxPaused) return;
        sfxSource.PlayOneShot(clip);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (clip == currentMusic && musicSource.isPlaying) return;
        currentMusic = clip;
        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        musicSource.Stop();
        currentMusic = null;
    }

    public void ApplySavedSettings()
    {
        SaveData data = SaveSystem.Load();
        preDuckVolume = data.musicVolume;
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

    public void EnterPause()
    {
        if (isDucked) return;
        preDuckVolume = SaveSystem.Load().musicVolume;
        isDucked = true;
        sfxPaused = true;
        SetMusicVolume(duckVolume);
    }

    public void ExitPause()
    {
        if (!isDucked) return;
        isDucked = false;
        sfxPaused = false;
        SetMusicVolume(SaveSystem.Load().musicVolume);
    }

    public void TempUnduck()
    {
        if (!isDucked) return;
        SetMusicVolume(preDuckVolume);
    }

    public void ReDuck()
    {
        if (!isDucked) return;
        SetMusicVolume(duckVolume);
    }
}