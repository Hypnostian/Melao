using UnityEngine;

// Proyectil de Pops: la "cuqui" que dispara con el power-up Cuquis. Vuela recto
// hacia donde mira Pops, daña a los enemigos (EnemyBase.TakeHit) y se destruye al
// chocar con el entorno o agotar su vida util. NO daña a Pops.
//
// Detecta por COMPONENTE (EnemyBase) en vez de por capa, asi funciona aunque los
// enemigos esten en capas distintas (Default/Platform). Movimiento por SphereCast
// para no atravesar enemigos finos a alta velocidad (igual que EnemyProjectile).
[RequireComponent(typeof(Rigidbody))]
public class CuquiProjectile : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 9f;
    public float lifeTime = 3f;

    [Header("Daño")]
    public int damage = 1;

    [Header("Capas")]
    [Tooltip("Entorno que detiene la cuqui (Ground + Wall por defecto).")]
    public LayerMask environmentLayer = (1 << 8) | (1 << 9);
    [Tooltip("Capa del jugador, para ignorarla y no chocar con Pops al salir.")]
    public LayerMask playerLayer = 1 << 7;

    [Header("Deteccion")]
    public float hitRadius = 0.25f;
    public float spawnGrace = 0.05f;

    // Color galleta/cuqui (dorado claro).
    public static readonly Color CuquiColor = new Color(0.96f, 0.84f, 0.42f, 1f);

    private Rigidbody rb;
    private Vector3 velocity;
    private float age;

    // Crea y lanza una cuqui. Si 'prefab' es null, genera una esfera dorada.
    public static CuquiProjectile Spawn(Vector3 position, Vector3 direction, float speed,
                                        GameObject prefab = null, float scale = 0.3f)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Cuqui";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var srcCol = go.GetComponent<Collider>();
            if (srcCol != null) Destroy(srcCol);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = MakeMaterial(CuquiColor);
        }

        var proj = go.GetComponent<CuquiProjectile>();
        if (proj == null) proj = go.AddComponent<CuquiProjectile>();

        var col = go.GetComponent<Collider>();
        if (col == null) col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;

        proj.speed = speed;
        proj.Launch(direction);
        return proj;
    }

    private static Material MakeMaterial(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
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
        Vector3 next = rb.position + velocity * Time.fixedDeltaTime;

        // Detecta contra todo MENOS el jugador; filtra por componente en Resolve.
        int mask = ~playerLayer.value;

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

        // Enemigo -> golpe y destruir la cuqui.
        var enemy = other.GetComponentInParent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeHit(damage);
            Destroy(gameObject);
            return true;
        }

        // Entorno solido -> destruir. (Tras la gracia inicial, para no morir al salir.)
        if (age >= spawnGrace && ((1 << other.gameObject.layer) & environmentLayer) != 0)
        {
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    private void OnTriggerEnter(Collider other) { Resolve(other); }
}
