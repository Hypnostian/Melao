using UnityEngine;

// Maneja el respawn del jugador desde el ultimo checkpoint (o el spawn inicial).
// Sistema reutilizable: cualquier hazard llama a Kill() o KillNow().
[DisallowMultipleComponent]
public class PlayerRespawn : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Si esta vacio, se usa la posicion inicial del transform al iniciar la escena.")]
    [SerializeField] private Transform initialSpawnPoint;

    [Header("Reset")]
    [Tooltip("Tiempo (segundos) en negro/sin control tras morir. 0 = instantaneo.")]
    [SerializeField] private float deathFreezeTime = 0.05f;

    [Tooltip("Si esta activado, congela el Rigidbody durante el freeze para evitar caidas.")]
    [SerializeField] private bool freezeRigidbodyOnDeath = true;

    private Vector3 currentSpawnPos;
    private Quaternion currentSpawnRot;
    private Rigidbody rb;
    private bool isRespawning;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (initialSpawnPoint != null)
        {
            currentSpawnPos = initialSpawnPoint.position;
            currentSpawnRot = initialSpawnPoint.rotation;
        }
        else
        {
            currentSpawnPos = transform.position;
            currentSpawnRot = transform.rotation;
        }
    }

    public void SetCheckpoint(Vector3 position, Quaternion rotation)
    {
        currentSpawnPos = position;
        currentSpawnRot = rotation;
    }

    public void SetCheckpoint(Transform t)
    {
        if (t == null) return;
        SetCheckpoint(t.position, t.rotation);
    }

    // Llamada estandar desde un hazard.
    public void Kill()
    {
        if (isRespawning) return;
        StartCoroutine(RespawnRoutine());
    }

    // Respawn inmediato sin freeze.
    public void KillNow()
    {
        if (isRespawning) return;
        DoRespawn();
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        isRespawning = true;

        bool restoreKinematic = false;
        if (freezeRigidbodyOnDeath && rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            restoreKinematic = true;
        }

        if (deathFreezeTime > 0f)
            yield return new WaitForSeconds(deathFreezeTime);

        DoRespawn();

        if (restoreKinematic && rb != null)
            rb.isKinematic = false;

        isRespawning = false;
    }

    private void DoRespawn()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = currentSpawnPos;
            rb.rotation = currentSpawnRot;
        }
        transform.SetPositionAndRotation(currentSpawnPos, currentSpawnRot);
    }
}
