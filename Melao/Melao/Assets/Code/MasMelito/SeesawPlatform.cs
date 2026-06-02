using UnityEngine;

// Plataforma de EQUILIBRIO INTEGRADA (Mas-melito: Mamut, SugarAlgodon, BiriBOM).
// Combina en UN solo script:
//   1) Balancin: se inclina hacia el lado donde esta el jugador (estable al
//      centro). Gira sobre su CENTRO en el eje Z del mundo (visible de lado).
//   2) Deslizamiento: como el PlayerController fija su velocidad X cada frame,
//      el slope solo no lo desliza; por eso EMPUJAMOS al jugador hacia el lado
//      bajo via PlayerController.externalPushX. Cuanto mas inclinada, mas
//      resbala (y puede caer). Se puede contrarrestar caminando en contra.
//   3) Salto: el jugador camina normal, pero si SALTA desde la plataforma
//      recibe un pequeño impulso extra (PlayerController.externalJumpBoost).
[RequireComponent(typeof(Rigidbody))]
public class SeesawPlatform : MonoBehaviour
{
    [Header("Balancin")]
    [Tooltip("Grados de inclinacion por unidad de descentrado del jugador.")]
    public float tiltPerUnit = 28f;
    [Tooltip("Inclinacion maxima (grados).")]
    public float maxTilt = 35f;
    public float tiltSpeed = 8f;
    public float returnSpeed = 4f;
    [Tooltip("Invierte el sentido si quedara al reves.")]
    public bool invert = false;
    [Tooltip("Zona muerta cerca del centro (grados) donde NO resbala: estable.")]
    public float deadZoneAngle = 3f;

    [Header("Deslizamiento")]
    [Tooltip("Velocidad maxima de deslizamiento hacia el lado bajo (a tilt maximo).")]
    public float slideSpeed = 5f;

    [Header("Salto")]
    [Tooltip("Impulso vertical extra al saltar desde esta plataforma.")]
    public float jumpBoost = 2.5f;

    [Header("Deteccion")]
    public LayerMask playerLayer = 1 << 7;
    [Min(0.1f)] public float detectHeight = 0.7f;

    private Rigidbody rb;
    private Collider col;
    private Quaternion baseRot;
    private Vector3 pivotWorld, armBase;
    private float currentAngle;
    private bool ready;
    private PlayerController2_5D current;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        col = GetComponentInChildren<Collider>();
        baseRot = transform.rotation;
    }

    private void Start()
    {
        pivotWorld = (col != null) ? col.bounds.center : transform.position;
        armBase = transform.position - pivotWorld;
        ready = true;
    }

    private void FixedUpdate()
    {
        if (!ready) return;

        Transform pt = DetectPlayerOnTop();
        var pc = (pt != null) ? pt.GetComponentInParent<PlayerController2_5D>() : null;

        // --- Inclinacion objetivo segun descentrado ---
        float targetAngle = 0f;
        if (pt != null)
        {
            float offset = pt.position.x - pivotWorld.x;
            targetAngle = Mathf.Clamp(-offset * tiltPerUnit * (invert ? -1f : 1f), -maxTilt, maxTilt);
        }
        float aSpeed = (pt != null) ? tiltSpeed : returnSpeed;
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, aSpeed * maxTilt * Time.fixedDeltaTime);

        // Aplicar la inclinacion girando sobre el centro (eje Z mundo).
        Quaternion tiltRot = Quaternion.AngleAxis(currentAngle, Vector3.forward);
        rb.MoveRotation(tiltRot * baseRot);
        rb.MovePosition(pivotWorld + tiltRot * armBase);

        // --- Empuje + boost al jugador encima ---
        if (pc != null)
        {
            current = pc;
            // Desliza hacia el lado bajo: angulo negativo = derecha abajo = +X.
            float t = Mathf.Abs(currentAngle) <= deadZoneAngle ? 0f : (-currentAngle / maxTilt);
            pc.externalPushX = Mathf.Clamp(t, -1f, 1f) * slideSpeed;
            pc.externalJumpBoost = jumpBoost;
        }
        else if (current != null)
        {
            ReleasePlayer();
        }
    }

    private Transform DetectPlayerOnTop()
    {
        if (col == null) return null;
        Bounds b = col.bounds;
        Vector3 center = new Vector3(b.center.x, b.max.y + detectHeight * 0.5f, b.center.z);
        Vector3 half = new Vector3(b.extents.x * 1.2f, detectHeight * 0.5f, b.extents.z * 1.2f);
        var hits = Physics.OverlapBox(center, half, transform.rotation, playerLayer, QueryTriggerInteraction.Ignore);
        return hits.Length > 0 ? hits[0].transform : null;
    }

    private void ReleasePlayer()
    {
        if (current != null)
        {
            current.externalPushX = 0f;
            current.externalJumpBoost = 0f;
            current = null;
        }
    }

    private void OnDisable() { ReleasePlayer(); }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponentInChildren<Collider>();
        if (c == null) return;
        Bounds b = c.bounds;
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.4f);
        Vector3 center = new Vector3(b.center.x, b.max.y + detectHeight * 0.5f, b.center.z);
        Gizmos.DrawWireCube(center, new Vector3(b.size.x, detectHeight, b.size.z));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(b.center, 0.15f);
    }
}
