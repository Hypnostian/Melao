using UnityEngine;

public class PlayerFacing : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform model;     // arrastra aquí el _pops_colors
    [SerializeField] private Rigidbody rb;        // arrastra el Rigidbody del Player
    [SerializeField] private Transform cameraRef; // arrastra la Main Camera (para mirar de frente)

    [Header("Configuración")]
    [SerializeField] private float idleFaceSpeed = 4f;  // velocidad para girar hacia la cámara
    [SerializeField] private float moveTurnSpeed = 8f;  // velocidad de rotación al moverse
    [SerializeField] private float minVelocityToTurn = 0.1f;

    private bool facingRight = true;

    void Update()
    {
        if (!rb || !model) return;

        float velX = rb.linearVelocity.x;

        // Si se está moviendo a la derecha
        if (velX > minVelocityToTurn)
        {
            facingRight = true;
            RotateTowards(Vector3.right);   // hacia X+
        }
        // Si se está moviendo a la izquierda
        else if (velX < -minVelocityToTurn)
        {
            facingRight = false;
            RotateTowards(Vector3.left);    // hacia X-
        }
        else
        {
            // Está quieta → mira hacia la cámara
            if (cameraRef)
            {
                Vector3 dir = (cameraRef.position - model.position).normalized;
                dir.y = 0; // ignora la altura
                RotateTowards(dir, idleFaceSpeed);
            }
        }
    }

    private void RotateTowards(Vector3 direction, float speed = -1f)
    {
        if (speed <= 0f) speed = moveTurnSpeed;
        Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
        model.rotation = Quaternion.Slerp(model.rotation, targetRot, Time.deltaTime * speed);
    }
}
