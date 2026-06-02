using UnityEngine;

public class LevelEndTrigger : MonoBehaviour
{
    [SerializeField] private string nextLevelName;
    [SerializeField] private string currentLevelName;

    private bool triggered;

    private void Start()
    {
        if (string.IsNullOrEmpty(currentLevelName))
            currentLevelName = gameObject.scene.name;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggered) return;
        triggered = true;

        // Transicion con fundido a negro y carga del siguiente nivel (el fader
        // marca el nivel completado y guarda el siguiente). Robusto: funciona
        // aunque no haya UIManager/SceneLoader en la escena.
        ScreenFader.GetOrCreate().FadeToLevel(nextLevelName, currentLevelName);
    }
}
