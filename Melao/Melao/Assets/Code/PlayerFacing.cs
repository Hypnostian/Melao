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

    [Header("Aire")]
    [Tooltip("Umbral de |velY| para considerar al jugador 'en el aire' cuando no podamos preguntarle al PlayerController.")]
    [SerializeField] private float airVerticalSpeedThreshold = 0.3f;

    private bool facingRight = true;
    private PlayerController2_5D playerController;

    void Awake()
    {
        // Si el Rigidbody apunta al jugador, intentamos cachear el controller
        // para saber si esta en el suelo sin depender de heuristicas.
        if (rb != null) playerController = rb.GetComponent<PlayerController2_5D>();
    }

    void Update()
    {
        if (!rb || !model) return;

        float velX = rb.linearVelocity.x;

        // Movimiento horizontal claro -> orienta el modelo en ese sentido.
        if (velX > minVelocityToTurn)
        {
            facingRight = true;
            RotateTowards(Vector3.right);
            return;
        }

        if (velX < -minVelocityToTurn)
        {
            facingRight = false;
            RotateTowards(Vector3.left);
            return;
        }

        // Sin velocidad horizontal significativa:
        //   - En el aire: mantener el ultimo facing (para que un wall jump no
        //     gire el modelo a frente-camara cuando el impulso X decae).
        //   - En el piso quieta: mirar hacia la camara (estilo 2.5D).
        bool inAir = playerController != null
            ? !playerController.IsGrounded
            : Mathf.Abs(rb.linearVelocity.y) > airVerticalSpeedThreshold;

        if (inAir)
        {
            RotateTowards(facingRight ? Vector3.right : Vector3.left);
            return;
        }

        if (cameraRef)
        {
            Vector3 dir = (cameraRef.position - model.position).normalized;
            dir.y = 0;
            RotateTowards(dir, idleFaceSpeed);
        }
    }

    private void RotateTowards(Vector3 direction, float speed = -1f)
    {
        if (speed <= 0f) speed = moveTurnSpeed;
        Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
        model.rotation = Quaternion.Slerp(model.rotation, targetRot, Time.deltaTime * speed);
    }
}
