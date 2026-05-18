using UnityEngine;

public class PlatformAutoAvoid : MonoBehaviour
{
    [Header("Detección")]
    public float detectionRadius = 1f;      // Distancia para detectar otra plataforma
    public LayerMask platformLayer;         // Solo plataformas
    
    [Header("Movimiento")]
    public MovingPlatformHorizontal mover;  // Referencia al script de movimiento

    private void Start()
    {
        if (mover == null)
            mover = GetComponent<MovingPlatformHorizontal>();
    }

    private void Update()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, platformLayer);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject != this.gameObject)
            {
                // Si se está acercando demasiado, invierte su dirección
                mover.speed = -mover.speed;

                // Opcionalmente mueve la plataforma un poco para separarlas
                transform.position += new Vector3(0.1f * Mathf.Sign(mover.speed), 0, 0);

                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
