using UnityEngine;

// Plataforma de EQUILIBRIO (seesaw/balancin): estable en el centro, pero cuando
// el jugador se para descentrado, se inclina hacia ese lado. En las esquinas se
// inclina mucho y el jugador RESBALA y cae (al agua, p.ej.).
// Para Mamut, SugarAlgodon, BiriBOM (Mas-melito).
//
// Gira alrededor de su CENTRO (no del pivote del FBX) sobre el eje Z del MUNDO,
// asi el balanceo se ve bien desde la camara lateral sin importar como este
// orientado/pivoteado el modelo.
[RequireComponent(typeof(Rigidbody))]
public class BalancePlatform : MonoBehaviour
{
    [Header("Inclinacion")]
    [Tooltip("Grados de inclinacion por unidad de descentrado del jugador.")]
    public float tiltPerUnit = 30f;
    [Tooltip("Inclinacion maxima (grados). >22 hace que el jugador resbale.")]
    public float maxTilt = 38f;
    [Tooltip("Velocidad con la que sigue la inclinacion objetivo.")]
    public float tiltSpeed = 7f;
    [Tooltip("Velocidad con la que vuelve a nivel cuando nadie esta encima.")]
    public float returnSpeed = 4f;
    [Tooltip("Invierte el sentido si quedara al reves.")]
    public bool invert = false;

    [Header("Deteccion del jugador")]
    public LayerMask playerLayer = 1 << 7;
    [Tooltip("Altura del volumen sobre la plataforma donde se detecta al jugador.")]
    [Min(0.1f)] public float detectHeight = 0.7f;

    private Rigidbody rb;
    private Collider col;
    private Quaternion baseRot;
    private Vector3 pivotWorld;   // centro de giro (fijo)
    private Vector3 armBase;      // vector pivote -> pivote del transform (en base)
    private float currentAngle;
    private bool ready;

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
        // El centro del collider es el punto de giro estable.
        pivotWorld = (col != null) ? col.bounds.center : transform.position;
        armBase = transform.position - pivotWorld;
        ready = true;
    }

    private void FixedUpdate()
    {
        if (!ready) return;

        float targetAngle = 0f;
        Transform p = DetectPlayerOnTop();
        if (p != null)
        {
            // Descentrado horizontal del jugador respecto al CENTRO (mundo X).
            float offset = p.position.x - pivotWorld.x;
            targetAngle = Mathf.Clamp(-offset * tiltPerUnit * (invert ? -1f : 1f), -maxTilt, maxTilt);
        }

        float speed = (p != null) ? tiltSpeed : returnSpeed;
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, speed * maxTilt * Time.fixedDeltaTime);

        // Inclina alrededor del eje Z del MUNDO, girando sobre el centro.
        Quaternion tiltRot = Quaternion.AngleAxis(currentAngle, Vector3.forward);
        rb.MoveRotation(tiltRot * baseRot);
        rb.MovePosition(pivotWorld + tiltRot * armBase);
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
