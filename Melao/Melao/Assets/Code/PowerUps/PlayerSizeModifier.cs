using UnityEngine;

// Maneja los power-ups de TAMANO de Pops:
//   - Chips    -> se vuelve PEQUENA (y su collider, al escalar el transform) para
//                 pasar por huecos estrechos.
//   - Merengue -> se vuelve GRANDE, con mas alcance horizontal y mas salto.
//
// Escala el transform raiz del jugador (lo que escala automaticamente la
// CapsuleCollider via lossyScale y el GroundCheck que es hijo), y ajusta los
// multiplicadores de velocidad/salto del PlayerController2_5D. Los efectos son
// temporales (duracion configurable) y MUTUAMENTE EXCLUYENTES: aplicar uno
// cancela el otro. ResetSize() vuelve al estado normal.
[DisallowMultipleComponent]
public class PlayerSizeModifier : MonoBehaviour
{
    public enum SizeState { Normal, Small, Big }

    [Header("Chips (pequena)")]
    [Tooltip("Escala relativa al tamano base cuando esta pequena.")]
    [Range(0.2f, 1f)] public float smallScale = 0.5f;
    [Tooltip("Multiplicador de velocidad horizontal mientras esta pequena.")]
    public float smallSpeedMult = 1f;
    [Tooltip("Multiplicador de salto mientras esta pequena.")]
    public float smallJumpMult = 1f;

    [Header("Merengue (grande)")]
    [Tooltip("Escala relativa al tamano base cuando esta grande.")]
    [Range(1f, 3f)] public float bigScale = 1.7f;
    [Tooltip("Multiplicador de velocidad horizontal mientras esta grande (mas alcance).")]
    public float bigSpeedMult = 1.3f;
    [Tooltip("Multiplicador de salto mientras esta grande (llega mas alto).")]
    public float bigJumpMult = 1.6f;

    public SizeState State { get; private set; } = SizeState.Normal;

    private float timer;
    private Vector3 baseScale;
    private PlayerController2_5D controller;

    private void Awake()
    {
        baseScale = transform.localScale;
        controller = GetComponent<PlayerController2_5D>();
    }

    public void ApplySmall(float duration) => Apply(SizeState.Small, duration);
    public void ApplyBig(float duration)   => Apply(SizeState.Big, duration);

    private void Apply(SizeState s, float duration)
    {
        State = s;
        timer = Mathf.Max(0.1f, duration);

        float scale = s == SizeState.Small ? smallScale     : bigScale;
        float spd   = s == SizeState.Small ? smallSpeedMult : bigSpeedMult;
        float jmp   = s == SizeState.Small ? smallJumpMult  : bigJumpMult;

        transform.localScale = baseScale * scale;
        if (controller != null)
        {
            controller.externalSpeedMultiplier = spd;
            controller.externalJumpMultiplier  = jmp;
        }
    }

    public void ResetSize()
    {
        State = SizeState.Normal;
        timer = 0f;
        transform.localScale = baseScale;
        if (controller != null)
        {
            controller.externalSpeedMultiplier = 1f;
            controller.externalJumpMultiplier  = 1f;
        }
    }

    private void Update()
    {
        if (State == SizeState.Normal) return;
        timer -= Time.deltaTime;
        if (timer <= 0f) ResetSize();
    }
}
