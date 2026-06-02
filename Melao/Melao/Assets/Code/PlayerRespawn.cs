using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRespawn : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Si esta vacio, se usa la posicion inicial del transform al iniciar la escena.")]
    [SerializeField] private Transform initialSpawnPoint;

    [Header("Vidas")]
    [SerializeField] private int maxLives = 3;
    private int currentLives;

    [Header("Audio")]
    [SerializeField] private AudioClip deathSound;

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

        currentLives = maxLives;
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

    private bool TryLoseLife()
    {
        currentLives--;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(deathSound);
        HUDController.Instance?.UpdateHearts(currentLives, maxLives);

        if (currentLives <= 0)
        {
            UIManager.Instance?.TriggerGameOver();
            return true;
        }
        return false;
    }

    public void Kill()
    {
        if (isRespawning) return;
        if (TryLoseLife()) return;
        StartCoroutine(RespawnRoutine());
    }

    public void KillNow()
    {
        if (isRespawning) return;
        if (TryLoseLife()) return;
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
        else
        {
            transform.SetPositionAndRotation(currentSpawnPos, currentSpawnRot);
        }
    }
}
