using UnityEngine;

// Bonon (Chocolala): enemigo tipo Goomba.
//  - Patrulla para bloquear el avance.
//  - Mata a Pops si lo toca de lado; se derrota de UN pisoton.
//  - Si ve a Pops cerca, se ACERCA hacia ella (no muy rapido) con un dash corto.
// Animaciones: Walk (base, loop), Dash (trigger), Jump (trigger).
public class BononEnemy : EnemyBase
{
    [Header("Bonon - Acercamiento")]
    [Tooltip("Distancia a la que empieza a acercarse a Pops.")]
    public float chaseRange = 4f;
    [Tooltip("Velocidad al acercarse (un poco mas que patrulla, NO un lanzazo).")]
    public float chaseSpeed = 3f;
    [Tooltip("Cada cuanto reproduce la animacion de Dash mientras persigue.")]
    public float dashAnimCooldown = 2.5f;

    private float dashAnimTimer;

    protected override void Awake()
    {
        base.Awake();
        hitsToDefeat = Mathf.Max(1, hitsToDefeat); // Bonon: 1
        health = hitsToDefeat;
    }

    protected override void AIUpdate()
    {
        if (dashAnimTimer > 0f) dashAnimTimer -= Time.deltaTime;

        if (PlayerInRange() && PlayerHorizontalDistance() <= chaseRange)
        {
            // Acercarse a Pops a velocidad controlada (no se "arrastra" rapidisimo).
            float saved = patrolSpeed;
            patrolSpeed = chaseSpeed;
            MoveTowardDir(PlayerSide());
            patrolSpeed = saved;

            if (dashAnimTimer <= 0f) { dashAnimTimer = dashAnimCooldown; SetTrigger("Dash"); }
            return;
        }

        Patrol();
    }

    protected override void OnDeath()
    {
        SetTrigger("Jump"); // saltito al ser derrotado (no hay clip de muerte)
    }
}
