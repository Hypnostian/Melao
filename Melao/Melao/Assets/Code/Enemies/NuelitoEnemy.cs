using UnityEngine;

// Ñuelito (Boronitas): soldado raso. Patrulla y, al ver a Pops, DISPARA aceite
// hirviendo (proyectil amarillo quemado), apuntando con precision.
//  - Mata a Pops si lo toca de lado; se derrota de un pisoton.
// Animaciones: Walk (base, loop), Shooting (trigger).
public class NuelitoEnemy : EnemyBase
{
    [Header("Ñuelito - Disparo")]
    [Tooltip("Distancia a la que ve a Pops y dispara.")]
    public float shootRange = 9f;
    [Tooltip("Cooldown entre disparos.")]
    public float shootCooldown = 1.6f;
    [Tooltip("Velocidad del proyectil de aceite (mas lento = se ve mejor).")]
    public float projectileSpeed = 7f;
    [Tooltip("Punto de salida (boca). Si es null, usa muzzleOffset.")]
    public Transform muzzle;
    [Tooltip("Offset desde el centro (x se multiplica por la direccion). Subelo si el disparo sale del piso.")]
    public Vector3 muzzleOffset = new Vector3(0.45f, 0.45f, 0f);
    [Tooltip("Prefab opcional del proyectil; si es null, crea una esfera amarilla.")]
    public GameObject projectilePrefab;
    [Tooltip("Exigir linea de vista despejada. Por defecto OFF para que SIEMPRE dispare si te ve.")]
    public bool requireLineOfSight = false;

    private float shootCdTimer;
    private bool aiming;

    protected override void Awake()
    {
        base.Awake();
        hitsToDefeat = Mathf.Max(1, hitsToDefeat); // 1
        health = hitsToDefeat;
    }

    protected override void AIUpdate()
    {
        if (shootCdTimer > 0f) shootCdTimer -= Time.deltaTime;

        if (player != null && PlayerHorizontalDistance() <= shootRange && HasLineOfSight())
        {
            aiming = true;
            dir = PlayerSide(); // encarar a Pops

            if (shootCdTimer <= 0f)
            {
                shootCdTimer = shootCooldown;
                SetTrigger("Shooting");
                Invoke(nameof(FireProjectile), 0.18f); // cuadra con la animacion
            }
            return; // se detiene para disparar
        }

        aiming = false;
        Patrol();
    }

    private bool HasLineOfSight()
    {
        if (player == null) return false;
        if (!requireLineOfSight) return true;
        Vector3 from = MuzzlePosition();
        Vector3 to = AimPoint();
        Vector3 d = to - from;
        return !Physics.Raycast(from, d.normalized, d.magnitude, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void FireProjectile()
    {
        if (dead || player == null) return;
        Vector3 from = MuzzlePosition();
        // SOLO horizontal: dispara recto a la izquierda o derecha (hacia el lado
        // de Pops). Nunca vertical ni diagonal. La bala viaja a la altura de la
        // boca, que esta a media altura del jugador -> lo impacta.
        Vector3 toDir = Vector3.right * (PlayerSide() >= 0 ? 1f : -1f);
        EnemyProjectile.Spawn(from, toDir, projectileSpeed, projectilePrefab);
    }

    private Vector3 MuzzlePosition()
    {
        if (muzzle != null) return muzzle.position;
        return transform.position + new Vector3(muzzleOffset.x * dir, muzzleOffset.y, muzzleOffset.z);
    }

    // Apunta al centro del jugador (preciso). Usa el collider si esta disponible.
    private Vector3 AimPoint()
    {
        var pc = player.GetComponentInChildren<Collider>();
        if (pc != null) return pc.bounds.center;
        return player.position + Vector3.up * 0.4f;
    }

    protected override void UpdateBaseAnim()
    {
        SetBool("Moving", !dead && !aiming);
    }
}
