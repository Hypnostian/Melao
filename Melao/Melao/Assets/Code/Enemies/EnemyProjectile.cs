using UnityEngine;

// Proyectil de enemigo (aceite de Ñuelito). Vuela recto, mata a Pops al tocarlo,
// y se destruye al chocar con el entorno o al agotar su vida util.
// Color por defecto: amarillo aceite "quemado" (ambar oscuro).
[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 11f;
    public float lifeTime = 4f;
    [Tooltip("Si > 0, aplica gravedad (arco). 0 = recto y preciso.")]
    public float gravity = 0f;

    [Header("Capas")]
    public LayerMask playerLayer = 1 << 7;
    public LayerMask environmentLayer = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11);

    [Header("Deteccion robusta")]
    public float hitRadius = 0.28f;
    [Tooltip("Tiempo inicial en el que NO choca con el entorno (para no morir al salir de la boca).")]
    public float spawnGrace = 0.06f;

    // Aceite quemado: ambar oscuro.
    public static readonly Color OilColor = new Color(0.82f, 0.58f, 0.07f, 1f);
    // Proyectiles de Don Perico (GDD): trozos de cebolla y tomate.
    public static readonly Color OnionColor  = new Color(0.45f, 0.78f, 0.22f, 1f); // cebolla (verde)
    public static readonly Color TomatoColor = new Color(0.90f, 0.16f, 0.13f, 1f); // tomate (rojo)

    private Rigidbody rb;
    private Vector3 velocity;
    private float age;

    // Crea y lanza un proyectil. 'tint' colorea la esfera generada (si no se pasa
    // prefab); por defecto usa el color de aceite (compatibilidad con Ñuelito).
    public static EnemyProjectile Spawn(Vector3 position, Vector3 direction, float speed,
                                        GameObject prefab = null, float scale = 0.28f,
                                        Color? tint = null)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "EnemyProjectile";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var srcCol = go.GetComponent<Collider>();
            if (srcCol != null) Destroy(srcCol);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = MakeOilMaterial(tint ?? OilColor);
        }

        var proj = go.GetComponent<EnemyProjectile>();
        if (proj == null) proj = go.AddComponent<EnemyProjectile>();

        var col = go.GetComponent<Collider>();
        if (col == null) col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;

        proj.speed = speed;
        proj.Launch(direction);
        return proj;
    }

    private static Material MakeOilMaterial(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.5f); }
        m.color = c;
        return m;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    public void Launch(Vector3 direction)
    {
        velocity = direction.normalized * speed;
        Destroy(gameObject, lifeTime);
    }

    private void FixedUpdate()
    {
        age += Time.fixedDeltaTime;
        if (gravity > 0f) velocity += Vector3.down * gravity * Time.fixedDeltaTime;

        Vector3 next = rb.position + velocity * Time.fixedDeltaTime;

        // Durante la gracia inicial solo detecta al jugador (no el entorno),
        // asi no muere al salir de la boca del enemigo.
        LayerMask mask = (age < spawnGrace) ? playerLayer : (playerLayer | environmentLayer);

        Vector3 seg = next - rb.position;
        float dist = seg.magnitude;
        if (dist > 0.0001f &&
            Physics.SphereCast(rb.position, hitRadius, seg.normalized, out RaycastHit hit, dist,
                               mask, QueryTriggerInteraction.Ignore))
        {
            if (Resolve(hit.collider)) return;
        }
        var hits = Physics.OverlapSphere(next, hitRadius, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++) { if (Resolve(hits[i])) return; }

        rb.MovePosition(next);
    }

    private bool Resolve(Collider other)
    {
        if (other == null) return false;
        if (((1 << other.gameObject.layer) & playerLayer) != 0 || other.transform.root.CompareTag("Player"))
        {
            var respawn = other.GetComponentInParent<PlayerRespawn>();
            if (respawn != null) respawn.Kill();
            Destroy(gameObject);
            return true;
        }
        if (((1 << other.gameObject.layer) & environmentLayer) != 0)
        {
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    private void OnTriggerEnter(Collider other) { Resolve(other); }
}
