using UnityEngine;

// El Baloncito rueda horizontalmente. Cuando detecta una pared/obstaculo
// en su direccion de avance, invierte el sentido. La muerte al tocar al
// jugador se delega en HazardKill (mismo GameObject).
public class BaloncitoRoller : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad horizontal (unidades/segundo).")]
    [Min(0f)] public float speed = 2f;

    [Tooltip("Direccion inicial: +1 derecha, -1 izquierda.")]
    public int initialDirection = 1;

    [Header("Deteccion de pared")]
    [Tooltip("Layers que cuentan como pared/limite y hacen que rebote.")]
    public LayerMask obstacleLayer = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11); // Ground + Wall + Platform + Changua

    [Tooltip("Distancia desde la cara del collider a la que comienza a detectar el rebote.")]
    [Min(0.01f)] public float wallProbeDistance = 0.08f;

    [Tooltip("Tiempo minimo entre rebotes (evita oscilacion infinita en una esquina).")]
    [Min(0.01f)] public float minTimeBetweenFlips = 0.1f;

    [Header("Rodado visual")]
    [Tooltip("Radio aproximado de la bola para calcular la rotacion al rodar.")]
    [Min(0.01f)] public float visualRadius = 0.5f;

    [Tooltip("Eje sobre el que rueda (en espacio local, mejor Z).")]
    public Vector3 rollAxis = new Vector3(0f, 0f, 1f);

    private Rigidbody rb;
    private Collider col;
    private int direction;
    private float lastFlipTime = -999f;
    private Bounds cachedBounds;

    private void Awake()
    {
        direction = initialDirection >= 0 ? 1 : -1;

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        col = GetComponentInChildren<Collider>();
    }

    private void FixedUpdate()
    {
        if (col != null) cachedBounds = col.bounds;

        // Check pared en la direccion actual.
        if (Time.time - lastFlipTime > minTimeBetweenFlips && DetectsWallAhead())
        {
            direction = -direction;
            lastFlipTime = Time.time;
        }

        Vector3 step = Vector3.right * direction * speed * Time.fixedDeltaTime;
        Vector3 target = transform.position + step;

        // Rotacion visual proporcional al avance.
        float circumference = 2f * Mathf.PI * visualRadius;
        float degrees = (step.x / circumference) * 360f;
        Quaternion rollDelta = Quaternion.AngleAxis(-degrees, rollAxis.normalized);

        if (rb != null)
        {
            rb.MovePosition(target);
            rb.MoveRotation(rb.rotation * rollDelta);
        }
        else
        {
            transform.position = target;
            transform.Rotate(rollAxis.normalized, -degrees, Space.Self);
        }
    }

    private bool DetectsWallAhead()
    {
        if (col == null) return false;

        Vector3 castDir = Vector3.right * direction;
        // Reducimos el slab vertical para no confundir el piso con una pared.
        Vector3 halfExt = new Vector3(0.02f, cachedBounds.extents.y * 0.6f, cachedBounds.extents.z * 0.9f);
        // Origen desde el centro del collider, no toca el piso.
        Vector3 origin = cachedBounds.center;

        // Collide en lugar de Ignore para que detecte la changua (trigger).
        return Physics.BoxCast(origin, halfExt, castDir, out _, Quaternion.identity,
                               cachedBounds.extents.x + wallProbeDistance, obstacleLayer,
                               QueryTriggerInteraction.Collide);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = direction >= 0 ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, Vector3.right * direction * 1f);
    }
}
