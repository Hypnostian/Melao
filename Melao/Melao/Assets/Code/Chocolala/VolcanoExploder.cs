using UnityEngine;

// Volcan: ciclo de Idle -> Warning -> Exploding -> Regenerating -> Idle...
// Durante "Exploding" verifica si el jugador esta en radio y lo mata.
// Durante "Regenerating" desactiva renderer y collider del propio objeto.
//
// Pensado para el "relleno" (pasted__pCube3) dentro de cada ChocoBREAK.
// Genera un LineRenderer en forma de circulo para que el jugador SIEMPRE
// vea el radio de la explosion (se ilumina mas durante warning/explode).
public class VolcanoExploder : MonoBehaviour
{
    [Header("Ciclo (segundos)")]
    [Min(0f)] public float idleDuration = 3f;
    [Min(0f)] public float warningDuration = 0.6f;
    [Min(0f)] public float explosionDuration = 0.3f;
    [Min(0f)] public float regenerateDuration = 1.5f;

    [Header("Sincronizacion")]
    [Tooltip("Desfase inicial para escalonar varios volcanes (segundos).")]
    public float startDelay = 0f;

    [Header("Daño")]
    [Tooltip("Radio en metros donde la explosion mata al jugador.")]
    [Min(0f)] public float explosionRadius = 0.45f;

    [Tooltip("Layers que cuentan como jugador.")]
    public LayerMask playerLayer = 1 << 7;

    [Header("Visual de impacto (escala)")]
    public float warningScale = 1.15f;
    public float explodingScale = 1.45f;

    [Header("Aviso visual del radio")]
    [Tooltip("Si esta activo, dibuja un circulo del tamaño del radio para que el jugador lo vea.")]
    public bool showRadiusIndicator = true;

    [Tooltip("Altura sobre el suelo del circulo indicador.")]
    public float indicatorHeight = 0.05f;

    [Tooltip("Numero de segmentos del circulo (32 = bastante suave).")]
    [Min(8)] public int indicatorSegments = 32;

    [Tooltip("Grosor de la linea del circulo.")]
    [Min(0f)] public float indicatorWidth = 0.04f;

    public Color colorIdle    = new Color(1f, 0.6f, 0f, 0.35f);
    public Color colorWarning = new Color(1f, 0.4f, 0f, 0.9f);
    public Color colorExplode = new Color(1f, 0.1f, 0f, 1f);

    private enum Phase { Idle, Warning, Exploding, Regenerating }
    private Phase phase = Phase.Idle;
    private float timer;
    private bool dealtDamageThisCycle;

    private Vector3 originalScale;
    private Renderer[] bodyRenderers;
    private Collider[] bodyColliders;

    private LineRenderer indicator;

    private void Awake()
    {
        originalScale = transform.localScale;
        bodyRenderers = GetComponentsInChildren<Renderer>(true);
        bodyColliders = GetComponentsInChildren<Collider>(true);
        timer = idleDuration + startDelay;

        if (showRadiusIndicator) CreateIndicator();
        ApplyIndicatorColor(colorIdle);
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if (phase == Phase.Exploding && !dealtDamageThisCycle)
            TryDealDamage();

        if (timer <= 0f) Advance();

        // Mantener el indicador en mundo (en caso de que la escala del padre cambie).
        if (indicator != null) UpdateIndicatorGeometry();
    }

    private void Advance()
    {
        switch (phase)
        {
            case Phase.Idle:
                phase = Phase.Warning;
                timer = warningDuration;
                transform.localScale = originalScale * warningScale;
                ApplyIndicatorColor(colorWarning);
                break;

            case Phase.Warning:
                phase = Phase.Exploding;
                timer = explosionDuration;
                transform.localScale = originalScale * explodingScale;
                dealtDamageThisCycle = false;
                ApplyIndicatorColor(colorExplode);
                break;

            case Phase.Exploding:
                phase = Phase.Regenerating;
                timer = regenerateDuration;
                SetActiveVisuals(false);
                ApplyIndicatorColor(Color.clear);
                break;

            case Phase.Regenerating:
                phase = Phase.Idle;
                timer = idleDuration;
                transform.localScale = originalScale;
                SetActiveVisuals(true);
                ApplyIndicatorColor(colorIdle);
                break;
        }
    }

    private void TryDealDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, playerLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            var respawn = hits[i].GetComponentInParent<PlayerRespawn>();
            if (respawn != null)
            {
                respawn.Kill();
                dealtDamageThisCycle = true;
                return;
            }
        }
    }

    private void SetActiveVisuals(bool on)
    {
        if (bodyRenderers != null)
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null && bodyRenderers[i] != indicator)
                    bodyRenderers[i].enabled = on;

        if (bodyColliders != null)
            for (int i = 0; i < bodyColliders.Length; i++)
                if (bodyColliders[i] != null) bodyColliders[i].enabled = on;

        if (on) transform.localScale = originalScale;
    }

    // ----------------------------------------------------
    //   INDICADOR VISUAL DEL RADIO
    // ----------------------------------------------------
    private void CreateIndicator()
    {
        var go = new GameObject("ExplosionRadiusIndicator");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        indicator = go.AddComponent<LineRenderer>();
        indicator.useWorldSpace = true;
        indicator.loop = true;
        indicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        indicator.receiveShadows = false;
        indicator.material = new Material(Shader.Find("Sprites/Default"));
        indicator.startWidth = indicatorWidth;
        indicator.endWidth = indicatorWidth;
        indicator.positionCount = indicatorSegments;

        UpdateIndicatorGeometry();
    }

    private void UpdateIndicatorGeometry()
    {
        if (indicator == null) return;
        indicator.positionCount = indicatorSegments;
        Vector3 center = transform.position;
        center.y += indicatorHeight - transform.position.y * 0f; // dejamos absoluta
        for (int i = 0; i < indicatorSegments; i++)
        {
            float a = (i / (float)indicatorSegments) * Mathf.PI * 2f;
            indicator.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(a) * explosionRadius,
                transform.position.y + indicatorHeight,
                center.z + Mathf.Sin(a) * explosionRadius));
        }
    }

    private void ApplyIndicatorColor(Color c)
    {
        if (indicator == null) return;
        indicator.startColor = c;
        indicator.endColor = c;
        indicator.enabled = c.a > 0.01f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    private void OnDestroy()
    {
        if (indicator != null && indicator.material != null)
            Destroy(indicator.material);
    }
}
