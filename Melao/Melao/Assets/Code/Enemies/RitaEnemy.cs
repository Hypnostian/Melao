using UnityEngine;

// Rita la frita (Chocolala/Mas-melito): jefa de final de nivel, infiltrada fria
// y astuta. GDD:
//   - "Patrulla lenta pero con rango de deteccion amplio."
//   - "Al ver a Pops, lanza embestidas laterales usando su cuerpo aplanado como
//      proyectil."
//   - "Mas resistente que Don Perico, requiere DOS golpes para ser eliminada."
//
// Implementacion (maquina de estados sobre EnemyBase):
//   PATRULLA   -> camina lento (Walk). Rango de deteccion amplio (detectRange).
//   AVISO      -> al ver a Pops se detiene, la encara y hace el wind-up (Splash)
//                 aplanando el cuerpo antes de embestir (Idle + Splash trigger).
//   EMBESTIDA  -> se lanza lateral y rapido hacia el lado de Pops (Walk rapido).
//   RECUPERAR  -> pausa breve tras chocar/terminar antes de repetir (Idle).
//
// Daño: contacto lateral / embestida mata a Pops. Se derrota con DOS pisotones.
// Animaciones: idle, Walk (Moving bool), Splash (trigger).
public class RitaEnemy : EnemyBase
{
    private enum Mode { Patrol, Telegraph, Charge, Recover }

    [Header("Rita - Patrulla lenta")]
    [Tooltip("Velocidad de patrulla (lenta, segun GDD).")]
    public float slowPatrolSpeed = 1.2f;

    [Header("Rita - Embestida (cuerpo aplanado)")]
    [Tooltip("Aviso/wind-up (Splash) antes de lanzarse.")]
    public float telegraphDuration = 0.5f;
    [Tooltip("Velocidad de la embestida lateral.")]
    public float chargeSpeed = 6.5f;
    [Tooltip("Duracion maxima de la embestida.")]
    public float chargeDuration = 1.0f;
    [Tooltip("Distancia maxima de una embestida.")]
    public float maxChargeDistance = 14f;
    [Tooltip("Pausa tras chocar/terminar la embestida antes de repetir.")]
    public float recoverDuration = 0.9f;

    [Header("Rita - Vida")]
    [Tooltip("Pisotones para derrotarla (GDD: dos golpes).")]
    [Min(1)] public int stompsToDefeat = 2;

    private Mode mode = Mode.Patrol;
    private float telegraphTimer;
    private int chargeDir = 1;
    private float chargeTimer;
    private float chargeStartX;
    private float recoverTimer;
    private bool engaging; // quieto (Idle) -> Moving=false

    protected override void Awake()
    {
        base.Awake();
        hitsToDefeat = Mathf.Max(1, stompsToDefeat);
        health = hitsToDefeat;
        patrolSpeed = slowPatrolSpeed;
        // Rango de deteccion amplio por defecto (GDD).
        if (detectRange < 10f) detectRange = 12f;
    }

    protected override void AIUpdate()
    {
        switch (mode)
        {
            case Mode.Patrol:    PatrolMode();    break;
            case Mode.Telegraph: TelegraphMode(); break;
            case Mode.Charge:    ChargeMode();    break;
            case Mode.Recover:   RecoverMode();   break;
        }
    }

    // -- PATRULLA -------------------------------------------------------
    private void PatrolMode()
    {
        engaging = false;
        Patrol();
        if (PlayerInRange()) EnterTelegraph();
    }

    // -- AVISO (wind-up Splash) ----------------------------------------
    private void EnterTelegraph()
    {
        mode = Mode.Telegraph;
        engaging = true;
        telegraphTimer = telegraphDuration;
        dir = PlayerSide();
        SetTrigger("Splash");   // aplana el cuerpo como aviso
    }

    private void TelegraphMode()
    {
        engaging = true;
        dir = PlayerSide();     // encara a Pops antes de lanzarse
        telegraphTimer -= Time.deltaTime;
        if (telegraphTimer <= 0f)
        {
            mode = Mode.Charge;
            engaging = false;
            chargeDir = PlayerSide();
            dir = chargeDir;
            chargeTimer = chargeDuration;
            chargeStartX = transform.position.x;
        }
    }

    // -- EMBESTIDA LATERAL ---------------------------------------------
    private void ChargeMode()
    {
        engaging = false;
        chargeTimer -= Time.deltaTime;
        float traveled = Mathf.Abs(transform.position.x - chargeStartX);

        if (WallAhead() || (turnAtLedges && LedgeAhead()) ||
            traveled >= maxChargeDistance || chargeTimer <= 0f)
        {
            EnterRecover();
            return;
        }

        float saved = patrolSpeed;
        patrolSpeed = chargeSpeed;
        MoveTowardDir(chargeDir);
        patrolSpeed = saved;
        dir = chargeDir;
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

        if (PlayerInRange()) EnterTelegraph();
        else mode = Mode.Patrol;
    }

    protected override void UpdateBaseAnim()
    {
        // Walk cuando se desplaza (patrulla o embestida); Idle en aviso/recuperar.
        SetBool("Moving", !dead && !engaging);
    }
}
