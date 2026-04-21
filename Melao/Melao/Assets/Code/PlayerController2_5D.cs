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
}

    void Update()
    {
        int combinedLayers = groundLayer | platformLayer;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, combinedLayers);
        if (isGrounded) lastTimeGrounded = Time.time;

        touchingWallLeft = false;
        touchingWallRight = false;

        if (!isGrounded)
        {
            Vector3 origin = transform.position + Vector3.up * (capsule.height * 0.4f);
            touchingWallLeft = Physics.Raycast(origin, Vector3.left, wallCheckDistance, wallLayer);
            touchingWallRight = Physics.Raycast(origin, Vector3.right, wallCheckDistance, wallLayer);

            if (touchingWallLeft || touchingWallRight)
            {
                lastTimeOnWall = Time.time;

                // ★ Reiniciar stick antes del slide
                wallStickCounter = wallStickTime;
            }
        }

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
    }

    private void TryPerformJump()
    {
        bool jumpBuffered = (Time.time - lastTimeJumpPressed) <= jumpBuffer;
        bool canCoyote = (Time.time - lastTimeGrounded) <= coyoteTime;
        bool canWallJump = !isGrounded && (Time.time - lastTimeOnWall) <= wallGraceTime;

        if (canWallJump && jumpBuffered)
        {
            animator.SetBool(ANIM_WALLJUMP, true);

            int dir = touchingWallLeft ? 1 : (touchingWallRight ? -1 : 0);

            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;

            Vector3 impulse = new Vector3(dir * wallJumpSideForce, wallJumpUpForce, 0f);
            rb.AddForce(impulse, ForceMode.VelocityChange);

            lastTimeJumpPressed = -999f;
            return;
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
        float horizontalSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("Speed", horizontalSpeed);

        animator.SetBool("IsGround", isGrounded);

        bool caminando = horizontalSpeed > 0.1f && isGrounded;
        animator.SetBool("Caminando", caminando);

        if (isGrounded)
        {
            animator.SetBool(ANIM_WALLJUMP, false);
        }
    }

    private void OnMove(InputValue value)
    {
        float inputX = value.Get<float>();
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
