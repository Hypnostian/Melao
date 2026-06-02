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
    [SerializeField] public float jumpForce = 4.5f;
    [SerializeField] private float coyoteTime = 0.10f;
    [SerializeField] private float jumpBuffer = 0.10f;

    [Header("Peso / Caida")]
    [Tooltip("Peso del personaje. Mayor peso = salta menos alto y cae mas rapido.")]
    [Range(0.5f, 3f)] [SerializeField] private float weight = 1.2f;

    [Tooltip("Multiplicador de gravedad adicional durante la caida (sensacion mas snappy de plataformero).")]
    [Range(1f, 4f)] [SerializeField] private float fallGravityMultiplier = 1.6f;

    [Header("Wall Jump")]
    [SerializeField] private float wallCheckDistance = 0.35f;
    [SerializeField] private float wallJumpUpForce = 4.5f;
    [SerializeField] private float wallJumpSideForce = 6.5f;
    [SerializeField] private float wallGraceTime = 0.1f;
    [SerializeField] private LayerMask wallLayer;

    [Tooltip("Tiempo tras un wall jump en el que el input horizontal se ignora. Hace que la trayectoria del wall jump se sienta comprometida y evita que el jugador anule su propio empuje.")]
    [Range(0f, 0.4f)] [SerializeField] private float wallJumpInputLockout = 0.15f;

    [Header("Detección Suelo")]
    [SerializeField] private LayerMask platformLayer; // PLatform
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.22f;

    [Header("Friccion")]
    [Tooltip("Friccion dinamica del jugador. 0 = se desliza por todo, 1 = se pega. 0.4 es buen plataformero.")]
    [Range(0f, 1f)] [SerializeField] private float dynamicFriction = 0.4f;
    [Tooltip("Friccion estatica (necesaria para no deslizarse en pendientes sin input).")]
    [Range(0f, 1f)] [SerializeField] private float staticFriction = 0.6f;

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
    private PhysicsMaterial playerMat;
    private bool frictionGrounded = true;
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

    // Cuenta atras del lockout de input tras un wall jump.
    private float wallJumpLockoutTimer = 0f;

    // Velocidad horizontal REAL del frame anterior (delta de posicion / dt).
    // Se usa para que la animacion no muestre "caminando" si la pared bloquea el movimiento.
    private float realHorizontalSpeed;
    private Vector3 lastFixedPos;

    // Lectura publica del estado de pisada (lo usa PlayerFacing para no rotar
    // hacia camara mientras esta en el aire).
    public bool IsGrounded => isGrounded;

    // Multiplicador de velocidad horizontal aplicado por sistemas externos
    // (ej. StickyFloor del Arequipe pegajoso). 1 = velocidad normal.
    [HideInInspector] public float externalSpeedMultiplier = 1f;

    // Empuje horizontal externo (unidades/seg) que se SUMA a la velocidad
    // objetivo. Usado por SeesawPlatform para deslizar al jugador hacia el lado
    // bajo. Se puede contrarrestar caminando en contra. 0 = sin empuje.
    [HideInInspector] public float externalPushX = 0f;

    // Impulso vertical EXTRA al saltar (lo da p.ej. SeesawPlatform). 0 = normal.
    [HideInInspector] public float externalJumpBoost = 0f;

void Awake()
{
    rb = GetComponent<Rigidbody>();
    capsule = GetComponent<CapsuleCollider>();

    // PhysicsMaterial con friccion DINAMICA (ver UpdateFriction):
    //  - En el suelo: friccion real -> no resbala en pendientes (queso, puente).
    //  - En el aire: friccion CERO -> NO se pega a paredes/plataformas al
    //    empujar contra ellas (cae directo). Combine = Minimum para que el 0
    //    domine y nunca se quede pegado.
    playerMat = new PhysicsMaterial("PlayerMaterial");
    playerMat.frictionCombine = PhysicsMaterialCombine.Minimum;
    playerMat.bounceCombine = PhysicsMaterialCombine.Minimum;
    playerMat.dynamicFriction = dynamicFriction; // estado inicial (suelo)
    playerMat.staticFriction = staticFriction;
    frictionGrounded = true;
    capsule.sharedMaterial = playerMat;

    if (groundCheck == null)
    {
        GameObject gc = new GameObject("GroundCheck");
        gc.transform.SetParent(transform);
        gc.transform.localPosition = new Vector3(0f, -capsule.height * 0.5f + 0.05f, 0f);
        groundCheck = gc.transform;
    }

    if (lockZ) fixedZ = transform.position.z;

    // Congelar TODAS las rotaciones del Rigidbody. La rotacion visual se hace
    // en el modelo hijo via PlayerFacing. Si Y queda libre, la friccion con el
    // suelo aplica torque sobre Y y el capsule rota lentamente sola.
    rb.constraints =
        RigidbodyConstraints.FreezeRotation |
        (lockZ ? RigidbodyConstraints.FreezePositionZ : 0);

    rb.interpolation = RigidbodyInterpolation.Interpolate;

    lastFixedPos = rb.position;
}

    void Update()
    {

        // --- DETECCION DE PARED (multi-altura, robusta) ---
        // Varios rayos a lo largo del capsule para detectar la pared de forma
        // fiable tanto si el jugador esta alto (cerca del tope) como bajo. NO se
        // lanzan rayos por debajo de los pies, para no dar falso positivo cuando
        // el jugador esta PARADO ENCIMA de una pared fina.
        touchingWallLeft  = CheckWall(Vector3.left);
        touchingWallRight = CheckWall(Vector3.right);
        bool touchingAnyWall = touchingWallLeft || touchingWallRight;

        // --- GROUND CHECK direccional (SphereCast hacia abajo vs Ground|Platform) ---
        int combinedLayers = groundLayer | platformLayer;
        Vector3 castOrigin = groundCheck.position + Vector3.up * 0.15f;
        float castRadius = Mathf.Max(0.05f, groundCheckRadius * 0.85f);
        isGrounded = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out _,
                                        0.3f, combinedLayers, QueryTriggerInteraction.Ignore);

        // Parado sobre la CIMA de una pared (Choquito): SOLO si NO estamos
        // pegados a una pared por el lado. Esto elimina el estado ambiguo
        // "grounded + touchingWall" que bugueaba el wall jump cerca del tope:
        //   - al lado de la pared (touchingWall) -> NO grounded -> wall jump.
        //   - genuinamente encima (sin contacto lateral) -> grounded -> salto normal.
        if (!isGrounded && !touchingAnyWall)
        {
            Vector3 rayOrigin = groundCheck.position + Vector3.up * 0.05f;
            float rayLen = groundCheckRadius + 0.15f;
            if (Physics.Raycast(rayOrigin, Vector3.down, rayLen, wallLayer, QueryTriggerInteraction.Ignore))
                isGrounded = true;
        }

        // Friccion dinamica: con suelo = friccion (no resbala en pendientes);
        // en el aire = cero (no se pega a paredes/plataformas al empujar).
        UpdateFriction(isGrounded);

        if (isGrounded)
        {
            lastTimeGrounded = Time.time;
            // Al pisar suelo, se reactiva el wall jump en cualquier lado.
            lastWallSideJumpedFrom = 0;
            // Al aterrizar, cancelar el lockout de input post-wall-jump.
            wallJumpLockoutTimer = 0f;
        }

        // Grace del wall jump + stick (solo en aire). lastTimeOnWall se refresca
        // mientras toquemos pared; el stick solo si el jugador presiona INTO.
        if (!isGrounded && touchingAnyWall)
        {
            lastTimeOnWall = Time.time;

            bool pressingIntoLeftWall = touchingWallLeft && moveInput.x < -0.1f;
            bool pressingIntoRightWall = touchingWallRight && moveInput.x > 0.1f;
            if (pressingIntoLeftWall || pressingIntoRightWall)
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
        // Durante el lockout post-wall-jump, ignoramos input para que la
        // trayectoria del wall jump no se anule pulsando contra la pared y
        // tampoco se mate por el wall slide.
        bool inLockout = wallJumpLockoutTimer > 0f;
        float effectiveInputX = moveInput.x;
        if (inLockout)
        {
            wallJumpLockoutTimer -= Time.fixedDeltaTime;
            effectiveInputX = 0f;
        }

        // Wall slide / stick es OPT-IN: solo aplica si el jugador esta
        // presionando INTO la pared (estilo Celeste/Hollow Knight). Si no
        // aprieta direccion, cae normal. Soluciona el "se queda pegado" en
        // cualquier pared / plataforma sin requerir checks por-objeto.
        bool pressingIntoLeftWall  = touchingWallLeft  && effectiveInputX < -0.1f;
        bool pressingIntoRightWall = touchingWallRight && effectiveInputX >  0.1f;
        bool pressingIntoWall = pressingIntoLeftWall || pressingIntoRightWall;

        // Empuje extra hacia abajo SOLO si el jugador esta intentando pegarse.
        if (!isGrounded && pressingIntoWall && rb.linearVelocity.y <= 0)
        {
            rb.AddForce(Vector3.down * 20f);
        }

        // WALL SLIDE -- solo si pressingIntoWall.
        if (!isGrounded && pressingIntoWall)
        {
            Vector3 vel = rb.linearVelocity;
            if (wallStickCounter > 0f)
            {
                vel.x = 0f;
                wallStickCounter -= Time.fixedDeltaTime;
            }
            else
            {
                vel.y = Mathf.Max(vel.y, wallSlideSpeed);
            }
            rb.linearVelocity = vel;
            return; // No procesar movimiento lateral mientras desliza
        }

        // Control horizontal. Durante el lockout NO tocamos la velocidad
        // horizontal: preservamos el impulso del wall jump para que su arco
        // COMPLETE y despegue de la pared. Antes el MoveTowards hacia 0 (input
        // muteado) decaia el empuje lateral, dejando un "wall jump incompleto"
        // que recaia sobre la misma pared = la muñeca se quedaba pegada.
        if (!inLockout)
        {
            // externalSpeedMultiplier ralentiza (StickyFloor); externalPushX
            // suma un empuje lateral (SeesawPlatform: deslizar al lado bajo).
            float targetSpeed = effectiveInputX * moveSpeed * externalSpeedMultiplier + externalPushX;
            float currentSpeed = rb.linearVelocity.x;

            float accel = isGrounded ? acceleration : acceleration * airControl;
            float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);

            Vector3 vel2 = rb.linearVelocity;
            vel2.x = newSpeed;
            rb.linearVelocity = vel2;
        }

        if (lockZ && Mathf.Abs(rb.position.z - fixedZ) > 0.0001f)
        {
            rb.position = new Vector3(rb.position.x, rb.position.y, fixedZ);
            Vector3 v = rb.linearVelocity;
            v.z = 0f;
            rb.linearVelocity = v;
        }

        // Gravedad adicional: peso > 1 anade gravedad constante; ademas, al caer
        // multiplicamos para sensacion mas snappy. Esto se aplica fuera del
        // wall slide para no romper esa logica.
        float extraGravityMult = (weight - 1f);
        if (rb.linearVelocity.y < 0f)
            extraGravityMult += (fallGravityMultiplier - 1f) * weight;
        if (extraGravityMult > 0f)
            rb.AddForce(Physics.gravity * extraGravityMult, ForceMode.Acceleration);

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

                // Wall jump escalado por peso: mas pesado = empuje vertical menor.
                Vector3 impulse = new Vector3(pushDir * wallJumpSideForce, wallJumpUpForce / weight, 0f);
                rb.AddForce(impulse, ForceMode.VelocityChange);

                // Lockout de input para que el wall jump se sienta comprometido.
                wallJumpLockoutTimer = wallJumpInputLockout;

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

            // Salto escalado por peso + impulso extra de plataforma (Seesaw).
            rb.AddForce(Vector3.up * (jumpForce / weight + externalJumpBoost), ForceMode.VelocityChange);

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

    // Detecta pared en 'dir' lanzando varios rayos a distintas alturas del
    // capsule. Excluye alturas por debajo de los pies para no dar falso positivo
    // cuando el jugador esta parado ENCIMA de una pared fina (la cima de un
    // Choquito) -- ahi debe contar como suelo, no como pared.
    private bool CheckWall(Vector3 dir)
    {
        float h = capsule.height;
        Vector3 c = transform.position;
        // Offsets relativos al centro del capsule (pies en -h*0.5).
        // El mas bajo (-h*0.20) sigue por encima de los pies para no rozar el
        // tope de la pared cuando estamos encima.
        float o0 = h * 0.40f;
        float o1 = h * 0.20f;
        float o2 = 0f;
        float o3 = -h * 0.20f;
        return Physics.Raycast(c + Vector3.up * o0, dir, wallCheckDistance, wallLayer, QueryTriggerInteraction.Ignore)
            || Physics.Raycast(c + Vector3.up * o1, dir, wallCheckDistance, wallLayer, QueryTriggerInteraction.Ignore)
            || Physics.Raycast(c + Vector3.up * o2, dir, wallCheckDistance, wallLayer, QueryTriggerInteraction.Ignore)
            || Physics.Raycast(c + Vector3.up * o3, dir, wallCheckDistance, wallLayer, QueryTriggerInteraction.Ignore);
    }

    // Friccion segun estado de suelo. En el aire = 0 (no se pega a paredes).
    private void UpdateFriction(bool grounded)
    {
        if (playerMat == null || grounded == frictionGrounded) return;
        frictionGrounded = grounded;
        playerMat.dynamicFriction = grounded ? dynamicFriction : 0f;
        playerMat.staticFriction = grounded ? staticFriction : 0f;
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
