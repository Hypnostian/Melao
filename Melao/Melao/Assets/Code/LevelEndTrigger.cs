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

        SaveSystem.MarkLevelComplete(currentLevelName);

        if (!string.IsNullOrEmpty(nextLevelName))
        {
            SaveSystem.SaveNextLevel(nextLevelName);
            UIManager.Instance.TriggerLevelComplete();
        }
        else
        {
            UIManager.Instance.TriggerGameComplete();
        }
    }
}
