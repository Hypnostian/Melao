using System.Collections.Generic;
using UnityEngine;

// Base de enemigos de Melao. Maneja lo comun a casi todos:
//  - Patrulla horizontal con giro en paredes y bordes (no se cae del borde).
//  - Snap al suelo (sigue el terreno aunque varie un poco de altura).
//  - Deteccion del jugador por rango.
//  - Contacto con el jugador:
//      * desde ARRIBA (pisoton) -> el enemigo recibe un golpe y rebota a Pops.
//      * desde el LADO          -> mata a Pops (PlayerRespawn.Kill()).
//  - Vida en "golpes para derrotar" (Bonon 1, Yucat varios, etc.).
//  - Helpers seguros para el Animator (no fallan si falta un parametro).
//
// Las subclases sobreescriben AIUpdate() para su comportamiento especifico
// (dash, ataque, disparo, etc.) y pueden llamar a Patrol() cuando toque.
[RequireComponent(typeof(Rigidbody))]
public class EnemyBase : MonoBehaviour
{
    [Header("Patrulla")]
    [Tooltip("Velocidad de patrulla horizontal.")]
    public float patrolSpeed = 2f;
    [Tooltip("Direccion inicial: +1 derecha, -1 izquierda.")]
    public int initialDirection = 1;
    [Tooltip("Si esta activo, gira al llegar a un borde (no se cae).")]
    public bool turnAtLedges = true;
    [Tooltip("Layers de suelo/pared para detectar borde, pared y hacer snap.")]
    public LayerMask groundLayer = (1 << 8) | (1 << 9) | (1 << 10);

    [Header("Orientacion")]
    [Tooltip("Si esta activo, el enemigo gira para mirar hacia su direccion de avance.")]
    public bool faceMovement = true;
    [Tooltip("Offset de yaw (grados) si el modelo no mira hacia +Z por defecto.")]
    public float yawOffset = 0f;

    [Header("Vida")]
    [Tooltip("Pisotones (o golpes) necesarios para derrotarlo.")]
    [Min(1)] public int hitsToDefeat = 1;

    [Header("Deteccion del jugador")]
    [Tooltip("Rango en el que el enemigo 've' a Pops.")]
    public float detectRange = 5f;
    public LayerMask playerLayer = 1 << 7;

    [Header("Pisoton")]
    [Tooltip("Impulso vertical que recibe Pops al pisar al enemigo.")]
    public float stompBounce = 8f;
    [Tooltip("Margen bajo la cima del enemigo donde el contacto cuenta como pisoton.")]
    public float stompTolerance = 0.15f;

    [Header("Plano 2.5D")]
    [Tooltip("Si esta activo, el enemigo se alinea al plano Z del jugador (mismo plano que Pops). Soluciona colisiones/golpes fallando por estar en distinto Z.")]
    public bool lockToPlayerZ = true;
    [Tooltip("Z fijo si no hay jugador o lockToPlayerZ esta apagado y se usa manual.")]
    public float manualZ = 0f;
    [Tooltip("Usar manualZ en vez del Z del jugador.")]
    public bool useManualZ = false;

    [Header("Muerte")]
    [Tooltip("Segundos antes de destruir el GameObject tras morir.")]
    public float destroyDelay = 1.2f;

    // --- runtime ---
    protected Animator animator;
    protected Rigidbody rb;
    protected Collider bodyCol;
    protected int dir;
    protected int health;
    protected bool dead;
    protected Vector3 authoredPos; // posicion que el script impone (autoridad sobre la animacion)
    protected Transform player;
    protected PlayerRespawn playerRespawn;
    protected Rigidbody playerRb;

    private HashSet<string> animParams;
    private float lastStompTime = -999f;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        animator = GetComponentInChildren<Animator>();
        bodyCol = GetComponentInChildren<Collider>();
        dir = initialDirection >= 0 ? 1 : -1;
        health = Mathf.Max(1, hitsToDefeat);
        authoredPos = transform.position;

        CacheAnimParams();
    }

    protected virtual void Start()
    {
        AcquirePlayer();
        // Snap inicial al suelo para no quedar flotando/hundido al spawnear.
        if (bodyCol != null) authoredPos = ApplyGroundSnap(transform.position);
    }

    protected virtual void Update()
    {
        if (dead) return;
        if (player == null) AcquirePlayer();

        AIUpdate();      // comportamiento especifico (por defecto: patrulla)
        UpdateFacing();
        UpdateBaseAnim();
    }

    protected virtual void LateUpdate()
    {
        if (dead) return;
        // Despues de la animacion: forzar la posicion autorizada por el script.
        // Anula cualquier deriva por root motion de las clips, y alinea Z al
        // plano del jugador. 'authoredPos' lo actualiza Patrol(); si el enemigo
        // esta quieto (observando/disparando) se queda donde estaba.
        float z = authoredPos.z;
        if (useManualZ) z = manualZ;
        else if (lockToPlayerZ && player != null) z = player.position.z;

        transform.position = new Vector3(authoredPos.x, authoredPos.y, z);
    }

    // Comportamiento por defecto: patrullar. Las subclases lo sobreescriben.
    protected virtual void AIUpdate()
    {
        Patrol();
    }

    // -----------------------------------------------------------------
    //   PATRULLA
    // -----------------------------------------------------------------
    protected void Patrol()
    {
        if (bodyCol == null)
        {
            authoredPos += Vector3.right * dir * patrolSpeed * Time.deltaTime;
            rb.MovePosition(authoredPos);
            return;
        }

        // Pared adelante -> girar.
        if (WallAhead()) dir = -dir;
        // Borde adelante (no hay suelo) -> girar.
        else if (turnAtLedges && LedgeAhead()) dir = -dir;

        Vector3 target = authoredPos + Vector3.right * dir * patrolSpeed * Time.deltaTime;
        target = ApplyGroundSnap(target);
        authoredPos = target;          // el script manda la posicion
        rb.MovePosition(target);       // y la fisica la sigue (contacto suave)
    }

    // Avanza hacia 'targetDir' (-1/+1) sin voltearse; se DETIENE si hay pared o
    // borde adelante (no se cae). Usado para perseguir/acercarse al jugador.
    protected void MoveTowardDir(int targetDir)
    {
        dir = targetDir >= 0 ? 1 : -1;
        if (bodyCol == null)
        {
            authoredPos += Vector3.right * dir * patrolSpeed * Time.deltaTime;
            rb.MovePosition(authoredPos);
            return;
        }
        if (WallAhead() || (turnAtLedges && LedgeAhead())) return; // no avanzar a pared/vacio

        Vector3 target = authoredPos + Vector3.right * dir * patrolSpeed * Time.deltaTime;
        target = ApplyGroundSnap(target);
        authoredPos = target;
        rb.MovePosition(target);
    }

    private Vector3 ApplyGroundSnap(Vector3 target)
    {
        Bounds b = bodyCol.bounds;
        Vector3 origin = new Vector3(target.x, b.center.y, b.center.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, b.extents.y + 0.6f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            float footOffset = transform.position.y - b.min.y; // pivote por encima de los pies
            target.y = hit.point.y + footOffset;
        }
        return target;
    }

    protected bool WallAhead()
    {
        Bounds b = bodyCol.bounds;
        Vector3 origin = new Vector3(b.center.x, b.center.y, b.center.z);
        float dist = b.extents.x + 0.1f;
        return Physics.Raycast(origin, Vector3.right * dir, dist, groundLayer, QueryTriggerInteraction.Ignore);
    }

    protected bool LedgeAhead()
    {
        Bounds b = bodyCol.bounds;
        // Punto justo delante del borde inferior, mirando hacia abajo.
        Vector3 origin = new Vector3(b.center.x + dir * (b.extents.x + 0.05f), b.min.y + 0.05f, b.center.z);
        return !Physics.Raycast(origin, Vector3.down, 0.4f, groundLayer, QueryTriggerInteraction.Ignore);
    }

    protected void UpdateFacing()
    {
        if (!faceMovement) return;
        float yaw = (dir >= 0 ? 90f : -90f) + yawOffset;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // -----------------------------------------------------------------
    //   DETECCION DEL JUGADOR
    // -----------------------------------------------------------------
    protected void AcquirePlayer()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return;
        player = go.transform;
        playerRespawn = go.GetComponentInParent<PlayerRespawn>();
        playerRb = go.GetComponent<Rigidbody>();
    }

    protected bool PlayerInRange()
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= detectRange;
    }

    // +1 si el jugador esta a la derecha, -1 si a la izquierda.
    protected int PlayerSide()
    {
        if (player == null) return dir;
        return player.position.x >= transform.position.x ? 1 : -1;
    }

    protected float PlayerHorizontalDistance()
    {
        if (player == null) return Mathf.Infinity;
        return Mathf.Abs(player.position.x - transform.position.x);
    }

    // -----------------------------------------------------------------
    //   CONTACTO / DAÑO
    // -----------------------------------------------------------------
    private void OnCollisionEnter(Collision collision) { HandleContact(collision.collider); }
    private void OnCollisionStay(Collision collision)  { HandleContact(collision.collider); }
    private void OnTriggerEnter(Collider other)        { HandleContact(other); }

    private void HandleContact(Collider other)
    {
        if (dead) return;
        if (!IsPlayer(other)) return;

        // Pisoton: el jugador toca por encima de la cima (con tolerancia) y viene cayendo.
        float playerY = other.bounds.min.y; // pies del jugador
        float enemyTop = bodyCol != null ? bodyCol.bounds.max.y : transform.position.y;
        bool falling = playerRb == null || playerRb.linearVelocity.y <= 0.5f;

        if (playerY >= enemyTop - stompTolerance && falling)
        {
            if (Time.time - lastStompTime < 0.2f) return;
            lastStompTime = Time.time;
            BouncePlayer();
            OnStomped();
            // Algunos enemigos (DonPerico) NO reciben daño por pisoton, solo por
            // la espalda. Por defecto el pisoton si daña (comportamiento clasico).
            if (StompDamagesEnemy()) TakeHit(1);
        }
        else
        {
            // Contacto lateral. Por defecto mata a Pops. Subclases como DonPerico
            // lo sobreescriben para recibir daño cuando el golpe viene por detras.
            OnSideContact(other);
        }
    }

    // Hooks de vulnerabilidad (defaults = comportamiento clasico de EnemyBase):
    //  - StompDamagesEnemy: si false, el pisoton rebota a Pops pero no daña.
    //  - OnSideContact: que pasa en un contacto lateral (no pisoton).
    protected virtual bool StompDamagesEnemy() => true;

    protected virtual void OnSideContact(Collider other)
    {
        KillPlayer();
    }

    protected bool IsPlayer(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0) return true;
        return other.transform.root.CompareTag("Player");
    }

    protected void BouncePlayer()
    {
        if (playerRb == null) return;
        Vector3 v = playerRb.linearVelocity;
        v.y = stompBounce;
        playerRb.linearVelocity = v;
    }

    protected void KillPlayer()
    {
        if (playerRespawn == null && player != null)
            playerRespawn = player.GetComponentInParent<PlayerRespawn>();
        if (playerRespawn != null) playerRespawn.Kill();
    }

    public virtual void TakeHit(int amount)
    {
        if (dead) return;
        health -= amount;
        if (health <= 0) Die();
        else OnHurt();
    }

    protected virtual void Die()
    {
        dead = true;
        OnDeath();
        // Desactivar colisiones para que no siga matando/bloqueando.
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        Destroy(gameObject, destroyDelay);
    }

    // Hooks para subclases (animaciones especificas).
    protected virtual void OnStomped() { }
    protected virtual void OnHurt() { }
    protected virtual void OnDeath() { }

    // 'Moving' por defecto: true si se esta desplazando. Las subclases pueden
    // sobreescribir UpdateBaseAnim para logica propia.
    protected virtual void UpdateBaseAnim()
    {
        SetBool("Moving", !dead);
    }

    // -----------------------------------------------------------------
    //   ANIMATOR helpers seguros
    // -----------------------------------------------------------------
    private void CacheAnimParams()
    {
        animParams = new HashSet<string>();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters) animParams.Add(p.name);
    }

    protected void SetBool(string name, bool value)
    {
        if (animator != null && animParams != null && animParams.Contains(name))
            animator.SetBool(name, value);
    }

    protected void SetTrigger(string name)
    {
        if (animator != null && animParams != null && animParams.Contains(name))
            animator.SetTrigger(name);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
