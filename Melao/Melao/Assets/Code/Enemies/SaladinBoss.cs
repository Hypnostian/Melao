using UnityEngine;

// Sir Saltín Saladín (Boronitas): jefe EMBESTIDOR simple.
//   - Espera en Idle hasta VER a Pops (rango de deteccion).
//   - Al verla: WARNING (animacion de aviso de que va a correr).
//   - Luego CORRE (Run) hacia Pops.
//   - Al chocar con una PARED o con POPS: CRASH (aturdido, vulnerable).
//   - Tras el aturdimiento: vuelve a Idle y repite si sigue viendo a Pops.
// NO lanza proyectiles. Solo corre y embiste.
//
// Daño: el contacto lateral/embestida mata a Pops. El jugador daña al jefe
// PISANDOLO mientras esta aturdido (Crash). Posicion bloqueada (X salvo carga,
// Y siempre, Z al plano del jugador) para que NO se teletransporte.
//
// Animaciones: Idle, Run cycle (Moving bool), Warning (trigger), Crash (trigger).
[RequireComponent(typeof(Rigidbody))]
public class SaladinBoss : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 4;

    [Header("Deteccion")]
    [Tooltip("Distancia a la que ve a Pops y empieza la secuencia de embestida.")]
    public float detectRange = 20f;
    public LayerMask playerLayer = 1 << 7;

    [Header("Aviso (Warning)")]
    [Tooltip("Duracion del aviso antes de arrancar a correr.")]
    public float warningDuration = 0.7f;

    [Header("Embestida (Run)")]
    [Tooltip("Velocidad de la embestida.")]
    public float chargeSpeed = 16f;
    [Tooltip("Distancia maxima de una embestida (por si no hay pared).")]
    public float maxChargeDistance = 16f;
    public LayerMask wallLayer = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11);

    [Header("Crash (aturdido / vulnerable)")]
    public float crashStunTime = 1.8f;

    [Header("Pisoton")]
    public float stompBounce = 9f;
    public float stompTolerance = 0.2f;

    [Header("Plano 2.5D")]
    public bool lockToPlayerZ = true;

    [Header("Feedback / Muerte")]
    [Tooltip("Parpadeo rojo al recibir un golpe (igual que el resto de enemigos).")]
    public bool flashOnHit = true;
    public Color hitFlashColor = new Color(1f, 0.2f, 0.2f, 1f);
    public float hitFlashDuration = 0.14f;
    [Tooltip("Segundos antes de desaparecer tras morir.")]
    public float destroyDelay = 1.5f;

    private enum S { Idle, Warning, Charge, Crash, Dead }
    private S state = S.Idle;
    private float timer;
    private bool vulnerable;
    private int health;
    private int chargeDir = 1;
    private float chargeStartX;
    private float lockedX, lockedY;
    private float lastHitTime = -999f;

    private Animator animator;
    private Rigidbody rb;
    private Collider bodyCol;
    private Transform player;
    private PlayerRespawn playerRespawn;
    private Rigidbody playerRb;
    private System.Collections.Generic.HashSet<string> animParams;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        animator = GetComponentInChildren<Animator>();
        bodyCol = GetComponentInChildren<Collider>();
        health = maxHealth;
        CacheParams();
    }

    private void Start()
    {
        AcquirePlayer();
        lockedX = transform.position.x;
        lockedY = transform.position.y;
        EnterState(S.Idle);
    }

    private void Update()
    {
        if (state == S.Dead) return;
        if (player == null) AcquirePlayer();

        FacePlayer();
        timer -= Time.deltaTime;

        switch (state)
        {
            case S.Idle:
                // Solo arranca si VE a Pops.
                if (PlayerInRange()) EnterState(S.Warning);
                break;

            case S.Warning:
                if (timer <= 0f)
                {
                    chargeDir = PlayerSide();
                    chargeStartX = transform.position.x;
                    EnterState(S.Charge);
                }
                break;

            case S.Charge:
                ChargeStep();
                break;

            case S.Crash:
                if (timer <= 0f)
                {
                    vulnerable = false;
                    // Si sigue viendo a Pops, vuelve a avisar; si no, descansa.
                    EnterState(PlayerInRange() ? S.Warning : S.Idle);
                }
                break;
        }

        UpdateAnim();
    }

    private void LateUpdate()
    {
        // Anula deriva de animacion: X fija salvo en carga, Y siempre, Z al plano.
        Vector3 p = transform.position;
        if (state != S.Charge) p.x = lockedX;
        p.y = lockedY;
        if (lockToPlayerZ && player != null) p.z = player.position.z;
        transform.position = p;
    }

    private void EnterState(S s)
    {
        state = s;
        switch (s)
        {
            case S.Idle:    vulnerable = false; SetBool("Moving", false); break;
            case S.Warning: timer = warningDuration; vulnerable = false; SetTrigger("Warning"); break;
            case S.Charge:  timer = 5f; vulnerable = false; break;
            case S.Crash:   timer = crashStunTime; vulnerable = true; SetTrigger("Crash"); break;
        }
    }

    private void ChargeStep()
    {
        float traveled = Mathf.Abs(transform.position.x - chargeStartX);
        if (WallAhead(chargeDir) || traveled >= maxChargeDistance || timer <= 0f)
        {
            EnterState(S.Crash);
            return;
        }
        rb.MovePosition(rb.position + Vector3.right * chargeDir * chargeSpeed * Time.deltaTime);
        lockedX = transform.position.x; // la carga SI mueve la X autorizada
    }

    private bool WallAhead(int d)
    {
        if (bodyCol == null) return false;
        Bounds b = bodyCol.bounds;
        return Physics.Raycast(b.center, Vector3.right * d, b.extents.x + 0.2f, wallLayer, QueryTriggerInteraction.Ignore);
    }

    // -----------------------------------------------------------------
    //   CONTACTO
    // -----------------------------------------------------------------
    private void OnCollisionEnter(Collision c) { Contact(c.collider); }
    private void OnCollisionStay(Collision c)  { Contact(c.collider); }
    private void OnTriggerEnter(Collider o)    { Contact(o); }

    private void Contact(Collider other)
    {
        if (state == S.Dead) return;
        if (!IsPlayer(other)) return;

        float playerY = other.bounds.min.y;
        float top = bodyCol != null ? bodyCol.bounds.max.y : transform.position.y;
        bool falling = playerRb == null || playerRb.linearVelocity.y <= 0.5f;

        if (playerY >= top - stompTolerance && falling)
        {
            // Pisoton: solo daña si esta aturdido (Crash). Siempre rebota a Pops.
            BouncePlayer();
            if (vulnerable && Time.time - lastHitTime > 0.3f)
            {
                lastHitTime = Time.time;
                Hit();
            }
        }
        else
        {
            // Contacto lateral / embestida: mata a Pops. Si embestia, ademas choca.
            KillPlayer();
            if (state == S.Charge) EnterState(S.Crash);
        }
    }

    private void Hit()
    {
        health--;
        FlashDamage();                 // feedback: parpadeo rojo (golpe acertado)
        if (health <= 0) Die();
    }

    private void Die()
    {
        state = S.Dead;
        SetBool("Moving", false);
        SetTrigger("Crash");
        foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = false;
        Destroy(gameObject, destroyDelay);   // desaparece tras morir (como los demas)
    }

    // -------- flash de daño (rojo) --------
    private Renderer[] flashRenderers;
    private MaterialPropertyBlock flashBlock;
    private Coroutine flashCo;

    private void FlashDamage()
    {
        if (!flashOnHit) return;
        if (flashRenderers == null) flashRenderers = GetComponentsInChildren<Renderer>(true);
        if (flashBlock == null) flashBlock = new MaterialPropertyBlock();
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine());
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        SetFlash(true);
        yield return new WaitForSeconds(hitFlashDuration);
        SetFlash(false);
    }

    private void SetFlash(bool on)
    {
        if (flashRenderers == null) return;
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            var r = flashRenderers[i];
            if (r == null) continue;
            if (on)
            {
                r.GetPropertyBlock(flashBlock);
                flashBlock.SetColor("_BaseColor", hitFlashColor);
                flashBlock.SetColor("_Color", hitFlashColor);
                r.SetPropertyBlock(flashBlock);
            }
            else r.SetPropertyBlock(null);
        }
    }

    private void BouncePlayer()
    {
        if (playerRb == null) return;
        Vector3 v = playerRb.linearVelocity; v.y = stompBounce; playerRb.linearVelocity = v;
    }

    private void KillPlayer()
    {
        if (playerRespawn == null && player != null)
            playerRespawn = player.GetComponentInParent<PlayerRespawn>();
        // Resta 1 corazon con empuje + i-frames (no mata de golpe).
        if (playerRespawn != null) playerRespawn.Damage(transform.position);
    }

    private bool IsPlayer(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0) return true;
        return other.transform.root.CompareTag("Player");
    }

    // -----------------------------------------------------------------
    //   UTIL
    // -----------------------------------------------------------------
    private void AcquirePlayer()
    {
        // Por componente, no por tag: no confundir a otro enemigo con Pops.
        var pc = FindFirstObjectByType<PlayerController2_5D>();
        GameObject go = pc != null ? pc.gameObject : GameObject.FindGameObjectWithTag("Player");
        if (go == null) return;
        player = go.transform;
        playerRespawn = go.GetComponentInParent<PlayerRespawn>();
        playerRb = go.GetComponent<Rigidbody>();
    }

    private bool PlayerInRange()
    {
        return player != null && Mathf.Abs(player.position.x - transform.position.x) <= detectRange;
    }

    private int PlayerSide() => (player != null && player.position.x < transform.position.x) ? -1 : 1;

    private void FacePlayer()
    {
        if (player == null) return;
        transform.rotation = Quaternion.Euler(0f, PlayerSide() >= 0 ? 90f : -90f, 0f);
    }

    private void UpdateAnim()
    {
        SetBool("Moving", state == S.Charge);
    }

    private void CacheParams()
    {
        animParams = new System.Collections.Generic.HashSet<string>();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters) animParams.Add(p.name);
    }
    private void SetBool(string n, bool v) { if (animator != null && animParams.Contains(n)) animator.SetBool(n, v); }
    private void SetTrigger(string n) { if (animator != null && animParams.Contains(n)) animator.SetTrigger(n); }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
