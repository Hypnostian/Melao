using UnityEngine;

// Don Perico (Mas-melito): jefe de final de nivel, soldado rudo y territorial.
// GDD:
//   - "Se desplaza de lado a lado de forma agresiva."
//   - "Al detectar a Pops, carga directo hacia ella."
//   - "Puede lanzar pequeños trozos de cebolla y tomate como proyectiles."
//   - "Vulnerable por la espalda."
//
// Implementacion (maquina de estados sobre EnemyBase):
//   PATRULLA  -> camina de lado a lado (Run).
//   APUNTAR   -> al ver a Pops se detiene, la encara y lanza una rafaga de
//                proyectiles alternando CEBOLLA (verde) y TOMATE (rojo) (Idle+Shoot).
//   CARGA     -> embiste recto hacia el lado donde estaba Pops (Run, rapido).
//                NO se re-orienta: asi Pops puede quedar a su ESPALDA.
//   RECUPERAR -> pausa breve tras chocar/terminar la carga (Idle), y repite.
//
// Daño:
//   - Solo es vulnerable POR LA ESPALDA (contacto del lado contrario al que mira).
//   - El pisoton NO le hace daño (solo rebota a Pops): hay que golpearlo por detras.
//   - El contacto de frente / embestida mata a Pops.
// Animaciones: Idle, Run (Moving bool), Shoot (trigger).
public class DonPericoEnemy : EnemyBase
{
    private enum Mode { Patrol, Aim, Charge, Recover }

    [Header("Don Perico - Patrulla agresiva")]
    [Tooltip("Velocidad de patrulla (de lado a lado).")]
    public float aggressivePatrolSpeed = 2.5f;

    [Header("Don Perico - Disparo (cebolla / tomate)")]
    [Tooltip("Distancia a la que se detiene a disparar proyectiles.")]
    public float shootRange = 9f;
    [Tooltip("Cuantos proyectiles lanza antes de cargar.")]
    [Min(1)] public int shotsPerVolley = 3;
    [Tooltip("Cooldown entre cada disparo de la rafaga.")]
    public float shootInterval = 0.55f;
    [Tooltip("Velocidad de los proyectiles (lenta para poder esquivar la rafaga).")]
    public float projectileSpeed = 5.5f;
    [Tooltip("Offset de la boca (x se multiplica por la direccion).")]
    public Vector3 muzzleOffset = new Vector3(0.5f, 0.55f, 0f);
    [Tooltip("Prefab opcional del proyectil; si es null crea una esfera coloreada.")]
    public GameObject projectilePrefab;

    [Header("Don Perico - Carga (embestida)")]
    [Tooltip("Velocidad de la embestida (carga directa hacia Pops).")]
    public float chargeSpeed = 6f;
    [Tooltip("Duracion maxima de una embestida.")]
    public float chargeDuration = 1.1f;
    [Tooltip("Pausa tras chocar/terminar la carga antes de volver a actuar.")]
    public float recoverDuration = 0.8f;

    [Header("Don Perico - Vida")]
    [Tooltip("Golpes por la espalda para derrotarlo (GDD: menos resistente que Rita).")]
    [Min(1)] public int backHitsToDefeat = 1;
    [Tooltip("Cooldown entre golpes por la espalda (anti multi-hit en un frame).")]
    public float backHitCooldown = 0.4f;

    private Mode mode = Mode.Patrol;
    private float aimTimer;
    private float shootTimer;
    private int shotsLeft;
    private int chargeDir = 1;
    private float chargeTimer;
    private float recoverTimer;
    private bool engaging;             // quieto (Idle) -> Moving=false
    private bool fireOnionNext = true; // alterna cebolla/tomate
    private float lastBackHitTime = -999f;

    protected override void Awake()
    {
        base.Awake();
        hitsToDefeat = Mathf.Max(1, backHitsToDefeat);
        health = hitsToDefeat;
        patrolSpeed = aggressivePatrolSpeed;
    }

    protected override void AIUpdate()
    {
        switch (mode)
        {
            case Mode.Patrol:   PatrolMode();   break;
            case Mode.Aim:      AimMode();      break;
            case Mode.Charge:   ChargeMode();   break;
            case Mode.Recover:  RecoverMode();  break;
        }
    }

    // -- PATRULLA -------------------------------------------------------
    private void PatrolMode()
    {
        engaging = false;
        Patrol();
        if (PlayerInRange()) EnterAim();
    }

    // -- APUNTAR / DISPARAR --------------------------------------------
    private void EnterAim()
    {
        mode = Mode.Aim;
        engaging = true;
        shotsLeft = Mathf.Max(1, shotsPerVolley);
        shootTimer = 0f;            // dispara casi de inmediato
        aimTimer = shotsPerVolley * shootInterval + 0.6f; // tope de seguridad
    }

    private void AimMode()
    {
        engaging = true;
        dir = PlayerSide();         // encarar a Pops mientras dispara

        aimTimer -= Time.deltaTime;
        shootTimer -= Time.deltaTime;

        if (shotsLeft > 0 && shootTimer <= 0f)
        {
            shootTimer = shootInterval;
            shotsLeft--;
            SetTrigger("Shoot");
            Invoke(nameof(FireProjectile), 0.16f); // cuadra con la animacion
        }

        bool volleyDone = shotsLeft <= 0 && shootTimer <= 0f;
        if (volleyDone || aimTimer <= 0f)
            EnterCharge();
    }

    private void FireProjectile()
    {
        if (dead || player == null) return;
        Vector3 from = transform.position + new Vector3(muzzleOffset.x * dir, muzzleOffset.y, muzzleOffset.z);
        Vector3 toDir = Vector3.right * (PlayerSide() >= 0 ? 1f : -1f);
        // Alterna cebolla (verde) y tomate (rojo) en cada disparo.
        Color tint = fireOnionNext ? EnemyProjectile.OnionColor : EnemyProjectile.TomatoColor;
        fireOnionNext = !fireOnionNext;
        EnemyProjectile.Spawn(from, toDir, projectileSpeed, projectilePrefab, 0.3f, tint);
    }

    // -- CARGA (embestida directa) -------------------------------------
    private void EnterCharge()
    {
        mode = Mode.Charge;
        engaging = false;
        chargeDir = PlayerSide();   // se compromete a este lado; ya no re-encara
        dir = chargeDir;
        chargeTimer = chargeDuration;
    }

    private void ChargeMode()
    {
        engaging = false;
        chargeTimer -= Time.deltaTime;

        // Embiste recto; se detiene en pared/borde o al agotar el tiempo.
        if (WallAhead() || (turnAtLedges && LedgeAhead()) || chargeTimer <= 0f)
        {
            EnterRecover();
            return;
        }

        float saved = patrolSpeed;
        patrolSpeed = chargeSpeed;
        MoveTowardDir(chargeDir);   // NO cambia 'dir' hacia Pops: deja la espalda expuesta
        patrolSpeed = saved;
        dir = chargeDir;            // mantener orientacion de la carga
    }

    // -- RECUPERAR ------------------------------------------------------
    private void EnterRecover()
    {
        mode = Mode.Recover;
        engaging = true;
        recoverTimer = recoverDuration;
    }

    private void RecoverMode()
    {
        engaging = true;
        recoverTimer -= Time.deltaTime;
        if (recoverTimer > 0f) return;

        if (PlayerInRange()) EnterAim();   // reinicia rafaga + timers
        else mode = Mode.Patrol;
    }

    // -- DAÑO: solo por la espalda; el pisoton no le hace daño ----------
    protected override bool StompDamagesEnemy() => false;

    protected override void OnSideContact(Collider other)
    {
        float enemyX = bodyCol != null ? bodyCol.bounds.center.x : transform.position.x;
        int playerSide = other.bounds.center.x >= enemyX ? 1 : -1;
        bool fromBehind = playerSide == -dir;   // su espalda esta en -dir

        if (fromBehind)
        {
            if (Time.time - lastBackHitTime < backHitCooldown) return;
            lastBackHitTime = Time.time;
            BouncePlayer();      // empuja a Pops hacia arriba para no re-matarla
            TakeHit(1);
        }
        else
        {
            KillPlayer();        // de frente / embestida: mortal
        }
    }

    protected override void UpdateBaseAnim()
    {
        // Run cuando se desplaza (patrulla o carga); Idle cuando dispara/recupera.
        SetBool("Moving", !dead && !engaging);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(0.95f, 0.8f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, shootRange);
    }
}
