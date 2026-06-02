using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Daño / invulnerabilidad")]
    [Tooltip("Tras recibir un golpe, segundos de invulnerabilidad. Garantiza que UN golpe = UN corazon (no drena toda la vida por contactos repetidos).")]
    [SerializeField] private float invulnDuration = 1f;
    [Tooltip("Empuje horizontal al recibir daño de espinas/enemigos (Damage). Pequeno.")]
    [SerializeField] private float knockbackForce = 4f;
    [Tooltip("Empuje vertical al recibir daño (para despegarlo del hazard).")]
    [SerializeField] private float knockbackUp = 2.5f;

    private Vector3 currentSpawnPos;
    private Quaternion currentSpawnRot;
    private Rigidbody rb;
    private PlayerSizeModifier sizeModifier;
    private bool isRespawning;
    private bool hasCheckpoint;
    private float invulnUntil = -999f;

    // True durante la ventana de invulnerabilidad tras un golpe.
    public bool IsInvulnerable => Time.time < invulnUntil;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sizeModifier = GetComponent<PlayerSizeModifier>();

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
        hasCheckpoint = true;
    }

    public void SetCheckpoint(Transform t)
    {
        if (t == null) return;
        SetCheckpoint(t.position, t.rotation);
    }

    // Resta una vida y respawnea. Si se acaban las 3 vidas, REINICIA con vidas
    // llenas en el checkpoint o, si no hay, al inicio del mapa. SIEMPRE respawnea:
    // nunca queda en un limbo "inmortal" (que era el bug al llegar a 0 vidas).
    private void HandleDeath(bool immediate)
    {
        currentLives--;
        invulnUntil = Time.time + invulnDuration;   // i-frames: 1 golpe = 1 corazon
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(deathSound);
        StartCoroutine(VibrateRoutine());

        if (currentLives <= 0)
        {
            GameOver(immediate);
            return;
        }

        HUDController.Instance?.UpdateHearts(currentLives, maxLives);
        Respawn(immediate);
    }

    // Daño que NO respawnea: resta 1 corazon, empuja al jugador hacia atras y le da
    // invulnerabilidad breve. Para espinas/enemigos: el jugador SIGUE jugando en su
    // sitio (no teletransporta al checkpoint). Si las vidas llegan a 0 -> reinicio.
    public void Damage(Vector3 sourcePosition)
    {
        if (isRespawning || IsInvulnerable) return;

        currentLives--;
        invulnUntil = Time.time + invulnDuration;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(deathSound);
        StartCoroutine(VibrateRoutine());
        ApplyKnockback(sourcePosition);

        if (currentLives <= 0)
        {
            GameOver(immediate: false);
            return;
        }

        HUDController.Instance?.UpdateHearts(currentLives, maxLives);
        // NO respawnea: sigue jugando con i-frames + knockback.
    }

    private void ApplyKnockback(Vector3 sourcePosition)
    {
        if (rb == null) return;
        float dir = Mathf.Sign(transform.position.x - sourcePosition.x);
        if (dir == 0f) dir = -1f;
        Vector3 v = rb.linearVelocity;
        v.x = dir * knockbackForce;             // empuje pequeno hacia atras
        v.y = Mathf.Max(v.y, knockbackUp);
        rb.linearVelocity = v;
    }

    // Se acabaron las vidas: reinicio. Funciona igual en CUALQUIER escena (no
    // depende de que existan UIManager/HUD; por eso vive aqui, en PlayerRespawn).
    private void GameOver(bool immediate)
    {
        currentLives = maxLives;                  // vuelve a tener las 3 vidas
        HUDController.Instance?.UpdateHearts(currentLives, maxLives);

        if (hasCheckpoint)
        {
            // Reinicio en el ultimo checkpoint (sin recargar: lo conserva).
            Respawn(immediate);
        }
        else if (SceneLoader.Instance != null)
        {
            // Sin checkpoint: reinicia el mapa desde el inicio (recarga la escena).
            Time.timeScale = 1f;
            SceneLoader.Instance.ReloadCurrentScene();
        }
        else
        {
            // Fallback (escena cargada suelta, sin SceneLoader): al spawn inicial.
            Respawn(immediate);
        }
    }

    private void Respawn(bool immediate)
    {
        if (immediate) DoRespawn();
        else StartCoroutine(RespawnRoutine());
    }

    // Recupera vidas (power-up Heart). No supera maxLives. Devuelve true si
    // realmente curo (estaba por debajo del maximo).
    public bool GainLife(int amount = 1)
    {
        if (amount <= 0 || currentLives >= maxLives) return false;
        currentLives = Mathf.Min(maxLives, currentLives + amount);
        HUDController.Instance?.UpdateHearts(currentLives, maxLives);
        return true;
    }

    public int CurrentLives => currentLives;
    public int MaxLives => maxLives;

    public void Kill()
    {
        if (isRespawning || IsInvulnerable) return;
        HandleDeath(immediate: false);
    }

    public void KillNow()
    {
        if (isRespawning || IsInvulnerable) return;
        HandleDeath(immediate: true);
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

    private System.Collections.IEnumerator VibrateRoutine()
    {
        if (!SaveSystem.Load().vibration) yield break;
        var gp = Gamepad.current;
        if (gp == null) yield break;
        gp.SetMotorSpeeds(0.8f, 0.6f);
        yield return new WaitForSecondsRealtime(0.2f);
        gp.SetMotorSpeeds(0f, 0f);
    }

    private void DoRespawn()
    {
        // Al morir/respawnear, Pops vuelve a su tamaño normal: nunca queda atascada
        // pequena/grande tras un power-up de tamano.
        if (sizeModifier != null) sizeModifier.ResetSize();

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
