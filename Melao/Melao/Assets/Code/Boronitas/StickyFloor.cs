using UnityEngine;

// Piso pegajoso (Arequipe(1)): mientras el jugador esta encima, reduce su
// velocidad horizontal mediante PlayerController2_5D.externalSpeedMultiplier.
// Debe ir en el mismo GameObject que tiene el collider solido (el setup tool
// lo agrega a cada collider del subarbol del Arequipe pegajoso).
[DisallowMultipleComponent]
public class StickyFloor : MonoBehaviour
{
    [Header("Pegajosidad")]
    [Tooltip("Multiplicador de velocidad mientras el jugador pisa (0.4 = 40% de la velocidad normal).")]
    [Range(0.05f, 1f)] public float speedMultiplier = 0.4f;

    [Tooltip("Layers que cuentan como jugador.")]
    public LayerMask playerLayer = 1 << 7;

    private PlayerController2_5D current;

    private void OnCollisionStay(Collision collision)
    {
        if (!IsPlayer(collision.collider)) return;
        if (current == null)
            current = collision.collider.GetComponentInParent<PlayerController2_5D>();
        if (current != null)
            current.externalSpeedMultiplier = speedMultiplier;
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!IsPlayer(collision.collider)) return;
        ReleasePlayer(collision.collider.GetComponentInParent<PlayerController2_5D>());
    }

    private void ReleasePlayer(PlayerController2_5D pc)
    {
        if (pc != null) pc.externalSpeedMultiplier = 1f;
        if (pc == current) current = null;
    }

    private void OnDisable()
    {
        // Por seguridad, restaurar al jugador si el objeto se desactiva mientras
        // el jugador estaba encima.
        if (current != null)
        {
            current.externalSpeedMultiplier = 1f;
            current = null;
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0) return true;
        return other.transform.root.CompareTag("Player");
    }
}
