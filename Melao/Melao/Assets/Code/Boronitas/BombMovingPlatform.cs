using UnityEngine;

// Bomba que se mueve verticalmente arriba/abajo de forma fluida (sinusoidal).
// Pensada para que el jugador la pise: usa Rigidbody kinematico + MovePosition.
//
// "Intercaladas": el setup tool alterna 'invertDirection' entre bombas vecinas
// para que cuando una sube, la de al lado baje.
//
// "No atraviesa nada": en Start hace un BoxCast desde FUERA del cuerpo, hacia
// arriba y hacia abajo, y recorta la amplitud efectiva contra obstaculos
// (Ground/Wall/Platform/Changua). Asi nunca penetra geometria.
[RequireComponent(typeof(Rigidbody))]
public class BombMovingPlatform : MonoBehaviour
{
    [Header("Movimiento vertical")]
    [Tooltip("Distancia maxima desde la posicion inicial, hacia arriba y hacia abajo.")]
    [Min(0f)] public float amplitude = 2f;

    [Tooltip("Velocidad angular del seno. Mayor = ciclo mas rapido.")]
    [Min(0f)] public float speed = 1.4f;

    [Header("Sincronizacion")]
    [Tooltip("Desfase en radianes. El setup tool lo deja en 0; usa invertDirection para alternar.")]
    public float phaseOffset = 0f;

    [Tooltip("Invierte el seno (alternar con la bomba vecina).")]
    public bool invertDirection = false;

    [Tooltip("Retraso antes de empezar a moverse (segundos). No afecta la fase global.")]
    [Min(0f)] public float startDelay = 0f;

    [Header("Anti-traspaso")]
    [Tooltip("Layers contra las que NO debe atravesar. Por defecto Ground+Wall+Platform+Changua.")]
    public LayerMask obstacleLayer = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11);

    [Tooltip("Margen de seguridad para no quedar pegado al obstaculo.")]
    [Min(0f)] public float clearance = 0.05f;

    private Vector3 startPos;
    private Rigidbody rb;
    private float effectiveAmplitudeUp;
    private float effectiveAmplitudeDown;
    private Collider[] ownColliders;

    private void Awake()
    {
        startPos = transform.position;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        ComputeSafeBounds();
    }

    // Cast desde FUERA del cuerpo (encima/debajo) para evitar el bug de iniciar
    // el cast dentro de un collider en overlap (que reporta "sin hit").
    private void ComputeSafeBounds()
    {
        effectiveAmplitudeUp = amplitude;
        effectiveAmplitudeDown = amplitude;

        if (!TryGetCombinedBounds(out Bounds b)) return;

        Vector3 halfExt = new Vector3(
            Mathf.Max(0.02f, b.extents.x * 0.9f),
            0.01f,
            Mathf.Max(0.02f, b.extents.z * 0.9f));

        float bodyHeight = b.size.y;
        const float buffer = 0.1f;
        float castDist = bodyHeight + amplitude + clearance + buffer * 2f;

        // CAST DOWN: arranca encima, atraviesa el cuerpo, busca abajo.
        // Se ignoran los colliders PROPIOS de la bomba (estan en layer Platform,
        // que tambien esta en obstacleLayer; sin este filtro la bomba se
        // detectaria a si misma y recortaria su amplitud a 0).
        Vector3 topOrigin = new Vector3(b.center.x, b.max.y + buffer, b.center.z);
        float distDown = NearestObstacleDistance(topOrigin, halfExt, Vector3.down, castDist);
        if (!float.IsInfinity(distDown))
        {
            float obstacleY = topOrigin.y - distDown;
            effectiveAmplitudeDown = Mathf.Max(0f, b.min.y - obstacleY - clearance);
        }

        // CAST UP: arranca debajo, atraviesa el cuerpo, busca arriba.
        Vector3 botOrigin = new Vector3(b.center.x, b.min.y - buffer, b.center.z);
        float distUp = NearestObstacleDistance(botOrigin, halfExt, Vector3.up, castDist);
        if (!float.IsInfinity(distUp))
        {
            float obstacleY = botOrigin.y + distUp;
            effectiveAmplitudeUp = Mathf.Max(0f, obstacleY - b.max.y - clearance);
        }
    }

    // Distancia al obstaculo mas cercano ignorando los colliders propios.
    // Devuelve Infinity si no hay obstaculo en el rango.
    private float NearestObstacleDistance(Vector3 origin, Vector3 halfExt, Vector3 dir, float maxDist)
    {
        var hits = Physics.BoxCastAll(origin, halfExt, dir, Quaternion.identity, maxDist,
                                      obstacleLayer, QueryTriggerInteraction.Collide);
        float best = Mathf.Infinity;
        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i].collider;
            if (c == null || IsOwnCollider(c)) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }
        return best;
    }

    private bool IsOwnCollider(Collider c)
    {
        if (ownColliders == null) return false;
        for (int i = 0; i < ownColliders.Length; i++)
            if (ownColliders[i] == c) return true;
        return false;
    }

    private bool TryGetCombinedBounds(out Bounds combined)
    {
        var colliders = GetComponentsInChildren<Collider>(false);
        bool found = false;
        combined = new Bounds(transform.position, Vector3.zero);
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null || c.isTrigger) continue;
            if (!found) { combined = c.bounds; found = true; }
            else combined.Encapsulate(c.bounds);
        }
        return found;
    }

    private void FixedUpdate()
    {
        if (Time.time < startDelay) return;

        float phase = Time.time * speed + phaseOffset;
        float raw = Mathf.Sin(phase);
        if (invertDirection) raw = -raw;

        float y = raw >= 0f ? raw * effectiveAmplitudeUp : raw * effectiveAmplitudeDown;
        rb.MovePosition(startPos + new Vector3(0f, y, 0f));
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? startPos : transform.position;
        float up = Application.isPlaying ? effectiveAmplitudeUp : amplitude;
        float down = Application.isPlaying ? effectiveAmplitudeDown : amplitude;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin + Vector3.up * up, origin - Vector3.up * down);
        Gizmos.DrawWireSphere(origin + Vector3.up * up, 0.1f);
        Gizmos.DrawWireSphere(origin - Vector3.up * down, 0.1f);
    }
}
