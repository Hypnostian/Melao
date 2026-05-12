using UnityEngine;

// Moneda-elevador: cuando el jugador esta encima, sube llevandoselo.
// Si choca con algo (ground, wall, platform, changua, etc) se RESETEA a su
// posicion inicial y vuelve a quedar a la espera.
//
// IMPORTANTE: detectamos al jugador con un OverlapBox cada FixedUpdate, no con
// OnCollisionEnter. La razon: un Rigidbody kinematico vs un Rigidbody dinamico
// no siempre dispara collision events confiablemente en Unity 6 cuando el
// jugador "camina" sobre la plataforma sin caer, o si el contacto se hace via
// CharacterController-style movement. El OverlapBox arriba de la moneda es
// determinista.
public class ElevatorMoneda : MonoBehaviour
{
    [Header("Elevacion")]
    [Tooltip("Velocidad de subida (unidades/segundo).")]
    [Min(0f)] public float riseSpeed = 3f;

    [Tooltip("Distancia maxima de subida; si la supera, se resetea (failsafe).")]
    [Min(0f)] public float maxRiseDistance = 15f;

    [Header("Capas")]
    [Tooltip("Layers que cuentan como jugador para iniciar la subida.")]
    public LayerMask playerLayer = 1 << 7;

    [Tooltip("Layers que detienen la moneda y la hacen resetear (Ground/Wall/Platform/Changua).")]
    public LayerMask stopLayers = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11);

    [Header("Deteccion")]
    [Tooltip("Altura del slab que detecta al jugador por encima del collider de la moneda.")]
    [Min(0.05f)] public float detectionHeight = 0.5f;

    [Tooltip("Cooldown tras un reset antes de aceptar otra activacion.")]
    [Min(0f)] public float reactivationDelay = 0.3f;

    private Vector3 startPos;
    private Quaternion startRot;
    private Rigidbody rb;
    private Collider col;
    private bool rising;
    private float lastResetTime = -999f;

    private void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        rb = GetComponent<Rigidbody>();
        col = GetComponentInChildren<Collider>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void FixedUpdate()
    {
        if (col == null) return;

        if (!rising)
        {
            if (Time.time - lastResetTime < reactivationDelay) return;
            if (DetectsPlayerOnTop()) rising = true;
            return;
        }

        // Mientras sube: chequear obstaculo por encima antes de moverse.
        Bounds b = col.bounds;
        Vector3 topOrigin = new Vector3(b.center.x, b.max.y, b.center.z);
        Vector3 sweepHalfExt = new Vector3(b.extents.x * 0.95f, 0.01f, b.extents.z * 0.95f);
        float stepDist = riseSpeed * Time.fixedDeltaTime + 0.1f;
        // Collide para incluir changua (trigger) entre las cosas que detienen la moneda.
        if (Physics.BoxCast(topOrigin, sweepHalfExt, Vector3.up, out _, Quaternion.identity,
                            stepDist, stopLayers, QueryTriggerInteraction.Collide))
        {
            ResetMoneda();
            return;
        }

        Vector3 target = transform.position + Vector3.up * riseSpeed * Time.fixedDeltaTime;
        if (rb != null) rb.MovePosition(target);
        else transform.position = target;

        if ((transform.position - startPos).magnitude >= maxRiseDistance)
            ResetMoneda();
    }

    private bool DetectsPlayerOnTop()
    {
        Bounds b = col.bounds;
        Vector3 center = new Vector3(b.center.x, b.max.y + detectionHeight * 0.5f, b.center.z);
        Vector3 halfExt = new Vector3(b.extents.x * 0.9f, detectionHeight * 0.5f, b.extents.z * 0.9f);
        return Physics.CheckBox(center, halfExt, Quaternion.identity,
                                playerLayer, QueryTriggerInteraction.Ignore);
    }

    private void ResetMoneda()
    {
        rising = false;
        lastResetTime = Time.time;
        if (rb != null)
        {
            rb.position = startPos;
            rb.rotation = startRot;
        }
        else
        {
            transform.SetPositionAndRotation(startPos, startRot);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? startPos : transform.position;

        // Linea de subida maxima.
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.7f);
        Gizmos.DrawLine(origin, origin + Vector3.up * maxRiseDistance);
        Gizmos.DrawWireSphere(origin + Vector3.up * maxRiseDistance, 0.1f);

        // Caja de deteccion del jugador.
        var c = GetComponentInChildren<Collider>();
        if (c != null)
        {
            Bounds b = c.bounds;
            Vector3 boxCenter = new Vector3(b.center.x, b.max.y + detectionHeight * 0.5f, b.center.z);
            Vector3 boxSize = new Vector3(b.size.x * 0.9f, detectionHeight, b.size.z * 0.9f);
            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
            Gizmos.DrawWireCube(boxCenter, boxSize);
        }
    }
}
