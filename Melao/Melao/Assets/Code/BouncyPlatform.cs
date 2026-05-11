using UnityEngine;

public class BouncyPlatform : MonoBehaviour
{
    [Header("Fuerza del rebote")]
    [SerializeField] private float bounceForce = 12f;

    [Header("Solo rebota si cae desde arriba")]
    [SerializeField] private bool onlyFromAbove = true;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        Rigidbody rb = collision.gameObject.GetComponent<Rigidbody>();
        if (rb == null) return;

        if (onlyFromAbove)
        {
            ContactPoint contact = collision.GetContact(0);

            // El jugador debe venir cayendo desde arriba
            // Dot mayor a 0.5 indica contacto desde arriba
            if (Vector3.Dot(contact.normal, Vector3.up) < 0.5f)
                return;
        }

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * bounceForce, ForceMode.VelocityChange);
    }
}
