using UnityEngine;

// Movimiento vertical sinusoidal para los objetos "gol".
// Reutilizable y configurable por par desde el Inspector.
//
// Como se usa para pares:
//  - Ambos gol del par usan misma 'speed' y misma 'amplitude'.
//  - Uno de los dos activa 'invertDirection' = true para que cuando uno suba,
//    el otro baje.
//  - Para mantener la sincronia entre pares distintos, se usa Time.time como
//    reloj global.
//
// El gol NO debe atravesar walls/ground. En Start hacemos un BoxCast hacia
// arriba y hacia abajo y limitamos la amplitud efectiva. Si la amplitud
// configurada cabe, queda igual; si chocaria con algo, se recorta automaticamente.
public class GolPairMover : MonoBehaviour
{
    [Header("Movimiento vertical")]
    [Tooltip("Distancia maxima desde la posicion inicial, hacia arriba y hacia abajo.")]
    [Min(0f)] public float amplitude = 2.5f;

    [Tooltip("Velocidad angular del seno. Mayor = ciclo mas rapido.")]
    [Min(0f)] public float speed = 1.5f;

    [Header("Sincronizacion")]
    [Tooltip("Desfase en radianes. Para alternar un par, dejar uno en 0 y el otro en PI (~3.1416).")]
    public float phaseOffset = 0f;

    [Tooltip("Atajo: invierte el seno (equivalente a phaseOffset = PI). Util para el segundo gol del par.")]
    public bool invertDirection = false;

    [Tooltip("Retraso adicional antes de empezar a moverse (segundos).")]
    [Min(0f)] public float startDelay = 0f;

    [Header("Suavizado")]
    [Tooltip("Si esta activado, se mueve el Rigidbody (kinematico) para que MovingPlatformMotionSender mida velocidad correctamente.")]
    public bool moveViaRigidbody = true;

    [Header("Anti-traspaso")]
    [Tooltip("Layers contra las que NO debe atravesar el gol. Por defecto Ground+Wall+Changua.")]
    public LayerMask obstacleLayer = (1 << 8) | (1 << 9) | (1 << 11);

    [Tooltip("Margen de seguridad para no quedar pegado al obstaculo.")]
    [Min(0f)] public float clearance = 0.05f;

    private Vector3 startPos;
    private Rigidbody rb;
    private float effectiveAmplitudeUp;
    private float effectiveAmplitudeDown;

    private void Awake()
    {
        startPos = transform.position;

        if (moveViaRigidbody)
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }
    }

    private void Start()
    {
        ComputeSafeBounds();
    }

    // Calcula que tan lejos puede subir y bajar el gol antes de chocar.
    // Estrategia: el BoxCast PARTE DESDE FUERA del cuerpo del gol (encima o
    // debajo), atraviesa el gol completo (no se detecta por estar en otro
    // layer) y reporta el primer obstaculo verdadero. Esto evita el bug donde,
    // si el gol arranca con un poco de overlap contra el suelo, el cast desde
    // adentro no reportaba hit y la amplitud quedaba sin recortar.
    private void ComputeSafeBounds()
    {
        effectiveAmplitudeUp = amplitude;
        effectiveAmplitudeDown = amplitude;

        Bounds b;
        if (!TryGetCombinedBounds(out b)) return;

        Vector3 halfExt = new Vector3(
            Mathf.Max(0.02f, b.extents.x * 0.9f),
            0.01f,
            Mathf.Max(0.02f, b.extents.z * 0.9f));

        float bodyHeight = b.size.y;
        const float buffer = 0.1f;
        float castDist = bodyHeight + amplitude + clearance + buffer * 2f;

        // CAST DOWN: arranca encima del gol, atraviesa su cuerpo, busca abajo.
        // QueryTriggerInteraction.Collide para detectar triggers como changua.
        Vector3 topOrigin = new Vector3(b.center.x, b.max.y + buffer, b.center.z);
        if (Physics.BoxCast(topOrigin, halfExt, Vector3.down, out RaycastHit hitDown,
                            Quaternion.identity, castDist, obstacleLayer,
                            QueryTriggerInteraction.Collide))
        {
            float obstacleY = topOrigin.y - hitDown.distance;
            effectiveAmplitudeDown = Mathf.Max(0f, b.min.y - obstacleY - clearance);
        }

        // CAST UP: arranca debajo del gol, atraviesa su cuerpo, busca arriba.
        Vector3 botOrigin = new Vector3(b.center.x, b.min.y - buffer, b.center.z);
        if (Physics.BoxCast(botOrigin, halfExt, Vector3.up, out RaycastHit hitUp,
                            Quaternion.identity, castDist, obstacleLayer,
                            QueryTriggerInteraction.Collide))
        {
            float obstacleY = botOrigin.y + hitUp.distance;
            effectiveAmplitudeUp = Mathf.Max(0f, obstacleY - b.max.y - clearance);
        }
    }

    // Une los bounds de TODOS los colliders del gol (maneja compound colliders).
    private bool TryGetCombinedBounds(out Bounds combined)
    {
        var colliders = GetComponentsInChildren<Collider>(includeInactive: false);
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

        // Escalar segun el lado: usa amplitud distinta para arriba y abajo.
        float y = raw >= 0f ? raw * effectiveAmplitudeUp : raw * effectiveAmplitudeDown;
        Vector3 target = startPos + new Vector3(0f, y, 0f);

        if (moveViaRigidbody && rb != null)
            rb.MovePosition(target);
        else
            transform.position = target;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? startPos : transform.position;
        float up = Application.isPlaying ? effectiveAmplitudeUp : amplitude;
        float down = Application.isPlaying ? effectiveAmplitudeDown : amplitude;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin + Vector3.up * up, origin - Vector3.up * down);
        Gizmos.DrawWireSphere(origin + Vector3.up * up, 0.1f);
        Gizmos.DrawWireSphere(origin - Vector3.up * down, 0.1f);
    }
}
