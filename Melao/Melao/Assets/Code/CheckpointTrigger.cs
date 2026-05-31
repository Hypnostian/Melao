using UnityEngine;

// Cuando el jugador entra al trigger, actualiza el spawn point de PlayerRespawn.
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("Layers que cuentan como jugador. Por defecto: solo el layer 'Player'.")]
    [SerializeField] private LayerMask playerLayer = 1 << 7;

    [Tooltip("Posicion de respawn. Si esta vacio se usa este mismo transform.")]
    [SerializeField] private Transform respawnAnchor;

    [Tooltip("Si esta activo, solo se puede activar una vez.")]
    [SerializeField] private bool oneShot = true;

    private bool used;

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (oneShot && used) return;
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null) return;

        Transform anchor = respawnAnchor != null ? respawnAnchor : transform;
        respawn.SetCheckpoint(anchor);
        used = true;
    }
}
