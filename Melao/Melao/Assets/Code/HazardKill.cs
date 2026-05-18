using UnityEngine;

// Hazard universal: cualquier objeto con este script mata al jugador al contacto.
// Reutilizable para spikes (QuesoPUNTEAGUDO), lava (Changua), gol, etc.
// Funciona tanto con collider solido (OnCollisionEnter) como con trigger (OnTriggerEnter).
[DisallowMultipleComponent]
public class HazardKill : MonoBehaviour
{
    [Header("Filtro de jugador")]
    [Tooltip("Layers que cuentan como jugador. Por defecto: solo el layer 'Player'.")]
    [SerializeField] private LayerMask playerLayer = 1 << 7;

    [Tooltip("Si esta activado, tambien acepta objetos con tag 'Player' (por compatibilidad con prefabs antiguos).")]
    [SerializeField] private bool alsoUseTag = true;

    [Header("Comportamiento")]
    [Tooltip("Pequeno cooldown para evitar matar dos veces al mismo player en el mismo frame.")]
    [SerializeField] private float retriggerCooldown = 0.25f;

    private float lastKillTime = -999f;

    private void OnTriggerEnter(Collider other)
    {
        TryKill(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryKill(collision.collider);
    }

    private void TryKill(Collider other)
    {
        if (Time.time - lastKillTime < retriggerCooldown) return;
        if (!IsPlayer(other)) return;

        PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null) return;

        lastKillTime = Time.time;
        respawn.Kill();
    }

    private bool IsPlayer(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0) return true;
        if (alsoUseTag && other.transform.root.CompareTag("Player")) return true;
        return false;
    }
}
