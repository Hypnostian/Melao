using UnityEngine;

public class PlayerPlatformReceiver : MonoBehaviour
{
    private Rigidbody rb;
    private MovingPlatformMotionSender currentPlatform;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            currentPlatform = col.gameObject.GetComponent<MovingPlatformMotionSender>();
        }
    }

    private void OnCollisionExit(Collision col)
    {
        if (col.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            if (currentPlatform != null && col.gameObject == currentPlatform.gameObject)
                currentPlatform = null;
        }
    }

    private void FixedUpdate()
    {
        if (currentPlatform != null)
        {
            // Añadir velocidad de la plataforma al rigidbody del player
            rb.position += currentPlatform.platformVelocity * Time.fixedDeltaTime;
        }
    }
}
