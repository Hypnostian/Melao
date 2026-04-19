using UnityEngine;

public class CheckpointIndicator : MonoBehaviour
{
    [SerializeField] private int checkpointIndex;

    private bool activated = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;
        if (!other.CompareTag("Player")) return;

        activated = true;
        HUDController.Instance.ActivateCheckpointDot(checkpointIndex);

        // TODO: SaveSystem.Save(checkpointIndex) cuando esté implementado
    }
}