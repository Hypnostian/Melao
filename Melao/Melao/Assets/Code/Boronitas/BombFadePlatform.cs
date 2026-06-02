using UnityEngine;
using System.Collections.Generic;

// Bomba "crumbling": apenas el jugador la toca empieza a desvanecerse; al
// terminar el fade desaparece (collider + renderer off) y, tras un tiempo,
// se regenera en su sitio.
//
// El fade de alpha intenta poner el material en modo transparente (URP). Si el
// shader no lo soporta visualmente, igual desaparece al final (funcional).
// El collider permanece activo durante el fade para que el jugador conserve
// el apoyo y caiga justo cuando la bomba desaparece.
public class BombFadePlatform : MonoBehaviour
{
    [Header("Tiempos (segundos)")]
    [Tooltip("Duracion del desvanecimiento tras el primer toque.")]
    [Min(0.05f)] public float fadeDuration = 0.8f;

    [Tooltip("Tiempo desaparecida antes de regenerarse.")]
    [Min(0.1f)] public float regenerateDelay = 2.5f;

    [Header("Deteccion del jugador")]
    [Tooltip("Layers que cuentan como jugador.")]
    public LayerMask playerLayer = 1 << 7;

    [Tooltip("Margen alrededor del cuerpo para detectar el toque.")]
    [Min(0f)] public float touchPadding = 0.06f;

    private enum State { Solid, Fading, Gone }
    private State state = State.Solid;
    private float timer;

    private Renderer[] renderers;
    private Collider[] colliders;
    private Material[] matInstances;
    private Color[] baseColors;
    private string[] colorProps;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        // Instanciar materiales para no tocar el asset compartido, y prepararlos
        // para transparencia.
        var mats = new List<Material>();
        var cols = new List<Color>();
        var props = new List<string>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var inst = r.materials; // copia -> instancias
            for (int i = 0; i < inst.Length; i++)
            {
                Material m = inst[i];
                string prop = m.HasProperty("_BaseColor") ? "_BaseColor"
                            : (m.HasProperty("_Color") ? "_Color" : null);
                MakeTransparent(m);
                mats.Add(m);
                cols.Add(prop != null ? m.GetColor(prop) : Color.white);
                props.Add(prop);
            }
            r.materials = inst;
        }
        matInstances = mats.ToArray();
        baseColors = cols.ToArray();
        colorProps = props.ToArray();
    }

    private void Update()
    {
        switch (state)
        {
            case State.Solid:
                if (PlayerIsTouching())
                {
                    state = State.Fading;
                    timer = fadeDuration;
                }
                break;

            case State.Fading:
                timer -= Time.deltaTime;
                SetAlpha(Mathf.Clamp01(timer / fadeDuration));
                if (timer <= 0f)
                {
                    SetActiveAll(false);
                    state = State.Gone;
                    timer = regenerateDelay;
                }
                break;

            case State.Gone:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    SetAlpha(1f);
                    SetActiveAll(true);
                    state = State.Solid;
                }
                break;
        }
    }

    private bool PlayerIsTouching()
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || !colliders[i].enabled) continue;
            Bounds b = colliders[i].bounds;
            Vector3 half = b.extents + Vector3.one * touchPadding;
            if (Physics.CheckBox(b.center, half, transform.rotation, playerLayer,
                                 QueryTriggerInteraction.Ignore))
                return true;
        }
        return false;
    }

    private void SetActiveAll(bool on)
    {
        foreach (var r in renderers) if (r != null) r.enabled = on;
        foreach (var c in colliders) if (c != null) c.enabled = on;
    }

    private void SetAlpha(float a)
    {
        if (matInstances == null) return;
        for (int i = 0; i < matInstances.Length; i++)
        {
            if (matInstances[i] == null || colorProps[i] == null) continue;
            Color c = baseColors[i];
            c.a = a;
            matInstances[i].SetColor(colorProps[i], c);
        }
    }

    private static void MakeTransparent(Material m)
    {
        if (m == null) return;
        // Configuracion URP Lit / Simple Lit para alpha blending.
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void OnDestroy()
    {
        if (matInstances == null) return;
        for (int i = 0; i < matInstances.Length; i++)
            if (matInstances[i] != null) Destroy(matInstances[i]);
    }
}
