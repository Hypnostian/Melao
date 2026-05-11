using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGamePickup : MonoBehaviour
{
    [SerializeField] private string creditsSceneName = "PanelCredits";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Cargar créditos
        SceneManager.LoadScene(creditsSceneName);
    }
}
