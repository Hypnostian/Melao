using UnityEngine;

public class SceneMusic : MonoBehaviour
{
    [SerializeField] private AudioClip musicClip;

    private void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMusic(musicClip);
    }
}
