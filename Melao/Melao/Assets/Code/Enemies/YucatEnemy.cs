using UnityEngine;

// Yucat (Chocolala): mas serio y estrategico. MUY DURO (varios pisotones).
//  - Patrulla si no ve a Pops.
//  - Si ve a Pops, SE ACERCA caminando hasta quedar a rango de golpe, y ahi
//    ATACA. (Antes se quedaba lejos pegando al aire.)
//  - Contacto lateral mata a Pops; se derrota con varios pisotones.
// Animaciones: Idle, Walk (Moving bool), Ataque (trigger).
public class YucatEnemy : EnemyBase
{
    [Header("Yucat - Combate")]
    [Tooltip("Pisotones necesarios (es duro).")]
    public int stompsToDefeat = 3;
    [Tooltip("Distancia a la que se DETIENE para atacar (debe ser <= alcance del golpe).")]
    public float stopDistance = 1.3f;
    [Tooltip("Cooldown entre ataques.")]
    public float attackCooldown = 1.4f;
    [Tooltip("Alcance horizontal del golpe.")]
    public float attackReach = 1.6f;

    private float attackCdTimer;
    private bool engaging; // quieto atacando (Moving=false)

    [Header("Yucat - Movimiento")]
    [Tooltip("Velocidad de Yucat (patrulla y acercamiento). Un poco mas rapido.")]
    public float moveSpeed = 4f;

    protected override void Awake()
    {
        base.Awake();
        hitsToDefeat = Mathf.Max(1, stompsToDefeat);
        health = hitsToDefeat;
        patrolSpeed = moveSpeed; // +2 sobre el default (2) del EnemyBase
    }

    protected override void AIUpdate()
    {
        if (attackCdTimer > 0f) attackCdTimer -= Time.deltaTime;

        if (PlayerInRange())
        {
            int side = PlayerSide();
            float dist = PlayerHorizontalDistance();

            if (dist > stopDistance)
            {
                // Acercarse caminando hacia Pops.
                engaging = false;
                MoveTowardDir(side);
            }
            else
            {
                // Lo bastante cerca: detenerse y atacar.
                engaging = true;
                dir = side;
                if (attackCdTimer <= 0f)
                {
                    attackCdTimer = attackCooldown;
                    SetTrigger("Ataque");
                    Invoke(nameof(ResolveAttack), 0.25f);
                }
            }
            return;
        }

        engaging = false;
        Patrol();
    }

    private void ResolveAttack()
    {
        if (dead) return;
        if (PlayerInRange() && PlayerHorizontalDistance() <= attackReach)
            KillPlayer();
    }

    protected override void UpdateBaseAnim()
    {
        // Moving = se desplaza (acercandose o patrullando), NO mientras ataca quieto.
        SetBool("Moving", !dead && !engaging);
    }
}
