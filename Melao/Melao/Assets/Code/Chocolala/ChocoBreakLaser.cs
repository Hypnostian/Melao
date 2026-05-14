using UnityEngine;

// Laser vertical entre dos chocobreaks alineados (uno abajo, otro arriba).
// Reemplaza el comportamiento de "explosion" del antiguo VolcanoExploder.
//
// Ciclo:
//   Idle (sin haz visible)
//   -> Warning (haz tenue como aviso)
//   -> Active (haz potente, mata al jugador si esta dentro del volumen)
//   -> Idle
//
// Detecta el chocobreak superior mediante un raycast hacia arriba al iniciar.
// Si no encuentra ninguno, usa una distancia fija como tope (configurable).
[DisallowMultipleComponent]
public class ChocoBreakLaser : MonoBehaviour
{
    [Header("Ciclo (segundos)")]
    [Min(0f)] public float idleDuration = 1.1f;
    [Min(0f)] public float warningDuration = 0.3f;
    [Min(0f)] public float activeDuration = 0.5f;

    [Header("Sincronizacion")]
    [Tooltip("Desfase inicial para escalonar varios laseres.")]
    public float startDelay = 0f;

    [Header("Detección del chocobreak superior")]
    [Tooltip("Direccion en la que se busca el chocobreak destino del haz (mundo).")]
    public Vector3 searchDirection = Vector3.up;

    [Tooltip("Distancia maxima del raycast de busqueda.")]
    [Min(0.5f)] public float maxSearchDistance = 20f;

    [Tooltip("Layers a chequear para encontrar el chocobreak destino (default Ground).")]
    public LayerMask searchLayer = (1 << 8);

    [Tooltip("Distancia fallback si no encuentra chocobreak destino.")]
    [Min(0.5f)] public float fallbackBeamLength = 6f;

    [Header("Daño")]
    [Tooltip("Mitad del ancho del haz (en X y Z). El haz mata al jugador que este dentro.")]
    [Min(0.05f)] public float beamHalfWidth = 0.35f;

    [Tooltip("Layers que cuentan como jugador.")]
    public LayerMask playerLayer = 1 << 7;

    [Header("Visual del haz")]
    [Tooltip("Color cuando el haz esta inactivo (alfa 0 = invisible).")]
    public Color colorIdle    = new Color(0.95f, 0.15f, 0.35f, 0.0f);
    [Tooltip("Color durante el aviso. Tono del relleno (rosa/rojo).")]
    public Color colorWarning = new Color(0.95f, 0.2f, 0.4f, 0.6f);
    [Tooltip("Color durante la explosion. Tono del relleno saturado.")]
    public Color colorActive  = new Color(1f, 0.1f, 0.35f, 1f);

    [Min(0.005f)] public float widthWarning = 0.18f;
    [Min(0.01f)] public float widthActive   = 0.7f;

    private enum Phase { Idle, Warning, Active }
    private Phase phase = Phase.Idle;
    private float timer;
    private bool damagedThisCycle;

    private Vector3 targetWorldPos;
    private bool targetFound;

    private LineRenderer beam;

    private void Awake()
    {
        CreateBeam();
        timer = idleDuration + startDelay;
        ApplyBeamColor(colorIdle, widthWarning);
    }

    private void Start()
    {
        ResolveTarget();
        UpdateBeamGeometry();
    }

    private void ResolveTarget()
    {
        Vector3 dir = searchDirection.normalized;
        // Origen ligeramente desplazado para evitar comenzar el ray dentro del
        // collider del propio relleno.
        Vector3 origin = transform.position + dir * 0.05f;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxSearchDistance, searchLayer,
                            QueryTriggerInteraction.Ignore))
        {
            targetWorldPos = hit.point;
            targetFound = true;
        }
        else
        {
            targetWorldPos = transform.position + dir * fallbackBeamLength;
            targetFound = false;
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if (phase == Phase.Active && !damagedThisCycle)
            TryDealDamage();

        if (timer <= 0f) Advance();

        // Mantener el haz en posicion (por si el objeto se movio en escena).
        UpdateBeamGeometry();
    }

    private void Advance()
    {
        switch (phase)
        {
            case Phase.Idle:
                phase = Phase.Warning;
                timer = warningDuration;
                ApplyBeamColor(colorWarning, widthWarning);
                break;

            case Phase.Warning:
                phase = Phase.Active;
                timer = activeDuration;
                damagedThisCycle = false;
                ApplyBeamColor(colorActive, widthActive);
                break;

            case Phase.Active:
                phase = Phase.Idle;
                timer = idleDuration;
                ApplyBeamColor(colorIdle, widthWarning);
                break;
        }
    }

    private void TryDealDamage()
    {
        Vector3 start = transform.position;
        Vector3 end = targetWorldPos;
        Vector3 center = (start + end) * 0.5f;
        Vector3 axis = end - start;
        float length = axis.magnitude;
        if (length < 0.01f) return;

        Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
        Vector3 halfExt = new Vector3(beamHalfWidth, length * 0.5f, beamHalfWidth);

        Collider[] hits = Physics.OverlapBox(center, halfExt, rot, playerLayer,
                                             QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var respawn = hits[i].GetComponentInParent<PlayerRespawn>();
            if (respawn != null)
            {
                respawn.Kill();
                damagedThisCycle = true;
                return;
            }
        }
    }

    private void CreateBeam()
    {
        var go = new GameObject("ChocoBreakLaserBeam");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        beam = go.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.numCapVertices = 2;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;
        beam.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void UpdateBeamGeometry()
    {
        if (beam == null) return;
        beam.SetPosition(0, transform.position);
        beam.SetPosition(1, targetWorldPos);
    }

    private void ApplyBeamColor(Color c, float width)
    {
        if (beam == null) return;
        beam.startColor = c;
        beam.endColor = c;
        beam.startWidth = width;
        beam.endWidth = width;
        beam.enabled = c.a > 0.01f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 dir = searchDirection.normalized;
        Vector3 origin = transform.position + dir * 0.05f;
        Vector3 endPos = Application.isPlaying ? targetWorldPos : origin + dir * maxSearchDistance;

        // Linea del haz (rojo translucido).
        Gizmos.color = new Color(1f, 0.2f, 0f, 0.6f);
        Gizmos.DrawLine(transform.position, endPos);

        // Volumen aproximado del haz como caja alineada.
        Vector3 axis = endPos - transform.position;
        float length = axis.magnitude;
        if (length > 0.01f)
        {
            Vector3 center = transform.position + axis * 0.5f;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
            Gizmos.color = new Color(1f, 0.2f, 0f, 0.25f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(beamHalfWidth * 2f, length, beamHalfWidth * 2f));
            Gizmos.matrix = prev;
        }
    }

    private void OnDestroy()
    {
        if (beam != null && beam.material != null) Destroy(beam.material);
    }
}
