using UnityEngine;

// Bomba que, apenas el jugador la toca, se dispara hacia ARRIBA.
// Si el jugador esta encima, lo lleva consigo (carry por contacto kinematico).
// Tras subir 'maxRiseDistance' (o chocar con algo) se resetea a su posicion
// inicial para poder reutilizarse.
//
// Deteccion por OverlapBox cada FixedUpdate (no OnCollisionEnter) para ser
// determinista con el Rigidbody del jugador.
[RequireComponent(typeof(Rigidbody))]
public class BombElevator : MonoBehaviour
{
    [Header("Disparo")]
    [Tooltip("Velocidad de subida (unidades/segundo).")]
    [Min(0f)] public float riseSpeed = 6f;

    [Tooltip("Distancia maxima de subida antes de resetear.")]
    [Min(0f)] public float maxRiseDistance = 12f;

    [Header("Deteccion del jugador")]
    [Tooltip("Layers que cuentan como jugador.")]
    public LayerMask playerLayer = 1 << 7;

    [Tooltip("Margen extra alrededor del cuerpo para detectar el toque del jugador.")]
    [Min(0f)] public float touchPadding = 0.08f;

    [Header("Anti-traspaso / reset")]
    [Tooltip("Layers que detienen la bomba y la hacen resetear.")]
    public LayerMask stopLayers = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11);

    [Tooltip("Cooldown tras el reset antes de poder volver a dispararse.")]
    [Min(0f)] public float reactivationDelay = 0.4f;

    private Vector3 startPos;
    private Quaternion startRot;
    private Rigidbody rb;
    private Collider col;
    private Collider[] ownColliders;
    private bool rising;
    private float lastResetTime = -999f;

    private void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        col = GetComponentInChildren<Collider>();
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private void FixedUpdate()
    {
        if (col == null) return;

        if (!rising)
        {
            if (Time.time - lastResetTime < reactivationDelay) return;
            if (PlayerIsTouching()) rising = true;
            return;
        }

        // Chequear obstaculo por encima antes de moverse, ignorando los
        // colliders propios (estan en Platform, que esta en stopLayers).
        Bounds b = col.bounds;
        Vector3 topOrigin = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        Vector3 sweepHalfExt = new Vector3(b.extents.x * 0.95f, 0.01f, b.extents.z * 0.95f);
        float stepDist = riseSpeed * Time.fixedDeltaTime + 0.1f;
        if (HasObstacleAbove(topOrigin, sweepHalfExt, stepDist))
        {
            ResetBomb();
            return;
        }

        rb.MovePosition(transform.position + Vector3.up * riseSpeed * Time.fixedDeltaTime);

        if ((transform.position - startPos).magnitude >= maxRiseDistance)
            ResetBomb();
    }

    private bool HasObstacleAbove(Vector3 origin, Vector3 halfExt, float dist)
    {
        var hits = Physics.BoxCastAll(origin, halfExt, Vector3.up, Quaternion.identity,
                                      dist, stopLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i].collider;
            if (c == null || IsOwnCollider(c)) continue;
            return true;
        }
        return false;
    }

    private bool IsOwnCollider(Collider c)
    {
        if (ownColliders == null) return false;
        for (int i = 0; i < ownColliders.Length; i++)
            if (ownColliders[i] == c) return true;
        return false;
    }

    private bool PlayerIsTouching()
    {
        Bounds b = col.bounds;
        Vector3 half = b.extents + Vector3.one * touchPadding;
        return Physics.CheckBox(b.center, half, transform.rotation, playerLayer,
                                QueryTriggerInteraction.Ignore);
    }

    private void ResetBomb()
    {
        rising = false;
        lastResetTime = Time.time;
        rb.position = startPos;
        rb.rotation = startRot;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? startPos : transform.position;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawLine(origin, origin + Vector3.up * maxRiseDistance);
        Gizmos.DrawWireSphere(origin + Vector3.up * maxRiseDistance, 0.12f);
    }
}
