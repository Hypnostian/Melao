using UnityEngine;

// Plataforma rebote (Pingu): el jugador rebota SOLO cuando aterriza por encima.
// La deteccion "desde arriba" se hace por POSICION del punto de contacto contra
// el techo del collider (bounds.max.y), no por la normal del contacto. Asi
// evita el bug del Pingu: el convex hull del Pingu suele tener caras
// inclinadas y la normal en la cima no apunta perfecto a arriba, por lo que
// la verificacion antigua (Dot con Vector3.up >= 0.5) podia fallar para
// landings legitimos y a la vez aceptar choques de costado tras desactivarla.
public class BouncyPlatform : MonoBehaviour
{
    [Header("Fuerza del rebote")]
    [SerializeField] private float bounceForce = 12f;

    [Header("Solo rebota si cae desde arriba")]
    [SerializeField] private bool onlyFromAbove = true;

    [Tooltip("Margen (en metros) desde la cara superior del collider para considerar 'desde arriba'. Mas grande = mas permisivo.")]
    [Min(0.01f)] [SerializeField] private float topTolerance = 0.12f;

    [Tooltip("Si esta activo, solo rebota si el jugador venia cayendo (velY < 0).")]
    [SerializeField] private bool requireFalling = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        Rigidbody rb = collision.gameObject.GetComponent<Rigidbody>();
        if (rb == null) return;

        if (requireFalling && rb.linearVelocity.y > 0.1f) return;

        if (onlyFromAbove && !ContactIsFromAbove(collision)) return;

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * bounceForce, ForceMode.VelocityChange);
    }

    // Para considerar el contacto como "desde arriba" se exigen DOS cosas:
    //   1) el punto de contacto esta dentro de 'topTolerance' del techo del
    //      collider (el glaseado); y
    //   2) la normal del contacto apunta predominantemente hacia arriba
    //      (normal.y > 0.5 = inclinacion menor a 60 grados).
    // Solo un contacto que cumpla AMBAS condiciones rebota.
    // Esto evita que choques laterales cerca del borde superior (donde el
    // punto si cae cerca del techo, pero la normal es horizontal) hagan que
    // el jugador se quede "flotando" pegado al costado del Pingu.
    private bool ContactIsFromAbove(Collision collision)
    {
        Collider myCol = GetComponent<Collider>();
        if (myCol == null) myCol = GetComponentInChildren<Collider>();
        if (myCol == null) return true; // sin collider de referencia, no podemos filtrar

        float topY = myCol.bounds.max.y;
        float positionThreshold = topY - topTolerance;
        const float normalThreshold = 0.5f;

        int n = collision.contactCount;
        for (int i = 0; i < n; i++)
        {
            ContactPoint cp = collision.GetContact(i);
            bool nearTop = cp.point.y >= positionThreshold;
            bool normalUp = cp.normal.y > normalThreshold;
            if (nearTop && normalUp) return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Collider myCol = GetComponent<Collider>();
        if (myCol == null) myCol = GetComponentInChildren<Collider>();
        if (myCol == null) return;

        Bounds b = myCol.bounds;
        // Slab que indica la zona "desde arriba" valida (el glaseado).
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.5f);
        Vector3 center = new Vector3(b.center.x, b.max.y - topTolerance * 0.5f, b.center.z);
        Vector3 size = new Vector3(b.size.x, topTolerance, b.size.z);
        Gizmos.DrawWireCube(center, size);
    }
}
