using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController2_5D : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 6.5f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float airControl = 0.6f;

    [Header("Salto")]
    [SerializeField] public float jumpForce = 8.5f;
    [SerializeField] private float coyoteTime = 0.10f;
    [SerializeField] private float jumpBuffer = 0.10f;

    [Header("Wall Jump")]
    [SerializeField] private float wallCheckDistance = 0.35f;
    [SerializeField] private float wallJumpUpForce = 8.5f;
    [SerializeField] private float wallJumpSideForce = 7.5f;
    [SerializeField] private float wallGraceTime = 0.04f;
    [SerializeField] private LayerMask wallLayer;

    [Header("Detección Suelo")]
    [SerializeField] private LayerMask platformLayer; // PLatform
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;

    [Header("Plano 2.5D")]
    [SerializeField] private bool lockZ = true;
    private float fixedZ;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string jumpTriggerParam = "JumpTrig";

    private static readonly int ANIM_WALLJUMP = Animator.StringToHash("WallJump");

    // --------------------------
    // ★ WALL SLIDE AÑADIDO ★
    // --------------------------
    [Header("Wall Slide")]
    [SerializeField] private float wallSlideSpeed = -2f;     // caída lenta
    [SerializeField] private float wallStickTime = 0.2f;     // tiempo pegado antes de deslizar
    private float wallStickCounter = 0f;
    // --------------------------

    [Header("Nivel (override por escena)")]
    [Tooltip("Si esta activado, A va a la derecha y D a la izquierda. Por defecto OFF; activar SOLO en escenas donde el nivel corre de derecha a izquierda.")]
    public bool invertHorizontalInput = false;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Vector2 moveInput;
    private bool jumpPressed;

    private bool isGrounded;
    private float lastTimeGrounded;
    private float lastTimeJumpPressed;

    private bool touchingWallLeft;
    private bool touchingWallRight;
    private float lastTimeOnWall;

    // -1 = izquierda, +1 = derecha, 0 = ninguna. Reseteado al tocar suelo o pared opuesta.
    private int lastWallSideJumpedFrom = 0;

    // Velocidad horizontal REAL del frame anterior (delta de posicion / dt).
    // Se usa para que la animacion no muestre "caminando" si la pared bloquea el movimiento.
    private float realHorizontalSpeed;
    private Vector3 lastFixedPos;

    // Lectura publica del estado de pisada (lo usa PlayerFacing para no rotar
    // hacia camara mientras esta en el aire).
    public bool IsGrounded => isGrounded;

void Awake()
{
    rb = GetComponent<Rigidbody>();
    capsule = GetComponent<CapsuleCollider>();

    //PhysicsMaterial con fricción 0 en el jugador Esto evita que el capsule se "pegue" a superficies laterales por fricción 
    PhysicsMaterial zeroFriction = new PhysicsMaterial("PlayerZeroFriction");
    zeroFriction.dynamicFriction = 0f;
    zeroFriction.staticFriction = 0f;
    zeroFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
    zeroFriction.bounceCombine = PhysicsMaterialCombine.Minimum;
    capsule.sharedMaterial = zeroFriction;

    if (groundCheck == null)
    {
        GameObject gc = new GameObject("GroundCheck");
        gc.transform.SetParent(transform);
        gc.transform.localPosition = new Vector3(0f, -capsule.height * 0.5f + 0.05f, 0f);
        groundCheck = gc.transform;
    }

    if (lockZ) fixedZ = transform.position.z;

    rb.constraints =
        RigidbodyConstraints.FreezeRotationX |
        RigidbodyConstraints.FreezeRotationZ |
        (lockZ ? RigidbodyConstraints.FreezePositionZ : 0);

    rb.interpolation = RigidbodyInterpolation.Interpolate;

    lastFixedPos = rb.position;
}

    void Update()
    {

        int combinedLayers = groundLayer | platformLayer;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, combinedLayers);

        // Caso especial: estar parado sobre la CIMA de una pared (ej. Choquito).
        // Un raycast estrictamente vertical evita falsos positivos al rozar el lado.
        if (!isGrounded)
        {
            Vector3 rayOrigin = groundCheck.position + Vector3.up * 0.05f;
            float rayLen = groundCheckRadius + 0.15f;
            if (Physics.Raycast(rayOrigin, Vector3.down, rayLen, wallLayer, QueryTriggerInteraction.Ignore))
                isGrounded = true;
        }

        if (isGrounded)
        {
            lastTimeGrounded = Time.time;
            // Al pisar suelo, se reactiva el wall jump en cualquier lado.
            lastWallSideJumpedFrom = 0;
        }

        // Wall raycast SIEMPRE (no solo en aire) para que la animacion
        // de caminata se suprima si estamos empujando contra una pared en el piso.
        Vector3 wallOrigin = transform.position + Vector3.up * (capsule.height * 0.4f);
        touchingWallLeft = Physics.Raycast(wallOrigin, Vector3.left, wallCheckDistance, wallLayer);
        touchingWallRight = Physics.Raycast(wallOrigin, Vector3.right, wallCheckDistance, wallLayer);

        // Logica de slide/grace solo aplica si NO esta en el suelo.
        if (!isGrounded && (touchingWallLeft || touchingWallRight))
        {
            lastTimeOnWall = Time.time;
            wallStickCounter = wallStickTime;
        }

        // Si tocamos la pared opuesta a la que usamos por ultima vez, reactivar wall jump.
        if (lastWallSideJumpedFrom == -1 && touchingWallRight) lastWallSideJumpedFrom = 0;
        if (lastWallSideJumpedFrom ==  1 && touchingWallLeft)  lastWallSideJumpedFrom = 0;

        if (jumpPressed)
        {
            lastTimeJumpPressed = Time.time;
            jumpPressed = false;
        }

        TryPerformJump();
        UpdateAnimations();
    }

    void FixedUpdate()
    {
        // Si no estás en wallLayer pero tocas algo lateral, fuerza caída
        if (!isGrounded && (touchingWallLeft || touchingWallRight) && rb.linearVelocity.y <= 0)
        {
            rb.AddForce(Vector3.down * 20f); // fuerza mínima para despegarlo
        }

        // --------------------------------------------------------
        // ★ WALL SLIDE / ANTI-PEGADO
        // --------------------------------------------------------
        if (!isGrounded && (touchingWallLeft || touchingWallRight))
        {
            Vector3 vel = rb.linearVelocity;

            if (wallStickCounter > 0f)
            {
                // Bloquea X un momento para evitar quedar pegado
                vel.x = 0f;
                wallStickCounter -= Time.fixedDeltaTime;
            }
            else
            {
                // Deslizamiento profesional
                vel.y = Mathf.Max(vel.y, wallSlideSpeed);
            }

            rb.linearVelocity = vel;
            return; // No procesar movimiento lateral mientras desliza
        }
        // --------------------------------------------------------

        float targetSpeed = moveInput.x * moveSpeed;
        float currentSpeed = rb.linearVelocity.x;

        float accel = isGrounded ? acceleration : acceleration * airControl;
        float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);

        Vector3 vel2 = rb.linearVelocity;
        vel2.x = newSpeed;
        rb.linearVelocity = vel2;

        if (lockZ && Mathf.Abs(rb.position.z - fixedZ) > 0.0001f)
        {
            rb.position = new Vector3(rb.position.x, rb.position.y, fixedZ);
            Vector3 v = rb.linearVelocity;
            v.z = 0f;
            rb.linearVelocity = v;
        }

        // Velocidad horizontal REAL (cuanto realmente se movio este step).
        float dx = rb.position.x - lastFixedPos.x;
        realHorizontalSpeed = Mathf.Abs(dx) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        lastFixedPos = rb.position;
    }

    private void TryPerformJump()
    {
        bool jumpBuffered = (Time.time - lastTimeJumpPressed) <= jumpBuffer;
        bool canCoyote = (Time.time - lastTimeGrounded) <= coyoteTime;
        bool canWallJump = !isGrounded && (Time.time - lastTimeOnWall) <= wallGraceTime;

        if (canWallJump && jumpBuffered)
        {
            // Lado de la pared que el jugador esta tocando (-1 izquierda, +1 derecha).
            int wallSide = touchingWallLeft ? -1 : (touchingWallRight ? 1 : 0);

            // Bloquear wall jump consecutivo desde la MISMA pared.
            // Se reactiva al pisar suelo o al tocar la pared opuesta (ver Update).
            if (wallSide == 0 || wallSide == lastWallSideJumpedFrom)
            {
                // No hay pared real o ya se uso esta: caemos al jump normal de abajo si aplica.
            }
            else
            {
                // WallJump como Trigger (auto-reset al consumirse).
                // Antes era Bool y la transicion AnyState->Walljump se re-disparaba
                // cada frame mientras el bool estaba en true, reseteando la
                // animacion al frame 0.
                animator.SetTrigger(ANIM_WALLJUMP);

                // Empuje hacia la direccion opuesta a la pared.
                int pushDir = -wallSide;

                Vector3 vel = rb.linearVelocity;
                vel.y = 0f;
                rb.linearVelocity = vel;

                Vector3 impulse = new Vector3(pushDir * wallJumpSideForce, wallJumpUpForce, 0f);
                rb.AddForce(impulse, ForceMode.VelocityChange);

                lastWallSideJumpedFrom = wallSide;
                lastTimeJumpPressed = -999f;
                return;
            }
        }

        if (jumpBuffered && canCoyote)
        {
            animator.SetTrigger(jumpTriggerParam);

            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

            lastTimeJumpPressed = -999f;
        }
    }

    private void UpdateAnimations()
    {
        // Usar la velocidad REAL (delta de posicion), no el target velocity.
        // Asi cuando una pared bloquea el movimiento, la animacion no se confunde.
        float speedForAnim = realHorizontalSpeed;
        animator.SetFloat("Speed", speedForAnim);

        animator.SetBool("IsGround", isGrounded);

        // 'Caminando' SIN requisito de isGrounded: asi cuando el player despega
        // saltando con movimiento horizontal, la animacion Jump_corriendo SI puede
        // dispararse (la transicion AnyState->Jump_corriendo exige Caminando=true
        // Y IsGround=false en el mismo frame).
        bool pressingIntoWall =
            (moveInput.x > 0.1f && touchingWallRight) ||
            (moveInput.x < -0.1f && touchingWallLeft);

        bool caminando = speedForAnim > 0.1f && !pressingIntoWall;
        animator.SetBool("Caminando", caminando);

        // (WallJump ya no es Bool: se dispara con SetTrigger arriba y se
        // auto-resetea cuando la transicion lo consume.)
    }

    private void OnMove(InputValue value)
    {
        float inputX = value.Get<float>();
        if (invertHorizontalInput) inputX = -inputX;
        moveInput = new Vector2(inputX, 0f);
    }

    private void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            jumpPressed = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * 0.4f;
        Gizmos.DrawLine(origin, origin + Vector3.left * wallCheckDistance);
        Gizmos.DrawLine(origin, origin + Vector3.right * wallCheckDistance);
    }
}
