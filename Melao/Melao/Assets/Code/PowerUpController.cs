using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Inventario y uso de power-ups de Pops (va en el Player).
//
// ENTRADA: el Player tiene un PlayerInput con Behavior = "Send Messages", que
// llama por nombre a OnUsePowerUp / OnNextPowerUp / OnPrevPowerUp (con InputValue).
//
// REGLAS (segun pedido):
//   - Se RECOGEN del mapa (PowerUpPickup) y quedan en el inventario.
//   - Solo se usan si estan en el inventario (gating estricto).
//   - SCENE-SCOPED: el inventario se vacia al cambiar de escena, asi que un
//     power-up solo se puede usar en el NIVEL donde se recogio.
//   - Cuquis, Chips y Merengue se pueden usar SIEMPRE (no se gastan); el Heart SI
//     se consume.
//   - Tecla F (boton Este) = usar el seleccionado.  E / Q (D-pad abajo/arriba) =
//     cambiar el power-up seleccionado.
//       * Cuquis  -> dispara una cuqui hacia donde mira Pops. 1 cada 3s (no rafaga).
//       * Heart   -> recupera una vida. Se consume.
//       * Chips   -> Pops pequena 15s, luego 15s de espera para reusar.
//       * Merengue-> Pops grande 15s (mas salto/alcance), luego 15s de espera.
[DisallowMultipleComponent]
public class PowerUpController : MonoBehaviour
{
    [System.Serializable]
    public struct PowerUpIcon
    {
        public PowerUpType type;
        public Sprite icon;
    }

    [Header("Iconos HUD (opcional, por tipo)")]
    [SerializeField] private PowerUpIcon[] icons;

    [Header("Cuquis (disparo controlado)")]
    [Tooltip("Prefab de la cuqui (el FBX de la cuqui). Si es null, dispara una esfera.")]
    [SerializeField] private GameObject cuquiProjectilePrefab;
    [SerializeField] private float cuquiSpeed = 9f;
    [Tooltip("Segundos entre disparos. 1 cuqui cada 3s.")]
    [SerializeField] private float cuquiCooldown = 3f;
    [Tooltip("Punto EXACTO de salida (opcional). Si se asigna (p.ej. un hijo en la mano), se usa este.")]
    [SerializeField] private Transform cuquiMuzzle;
    [Tooltip("Si no hay 'cuquiMuzzle': offset desde el centro de Pops (x se voltea segun mira). El pivote de Pops esta en su centro, asi que Y pequeno = altura de la mano.")]
    [SerializeField] private Vector3 cuquiMuzzleOffset = new Vector3(0.22f, 0.05f, 0f);

    [Header("Chips (pequena) - reutilizable")]
    [SerializeField] private float chipsDuration = 15f;
    [SerializeField] private float chipsCooldown = 15f;

    [Header("Merengue (grande) - reutilizable")]
    [SerializeField] private float merengueDuration = 15f;
    [SerializeField] private float merengueCooldown = 15f;

    // Inventario: lista ordenada de tipos poseidos + cantidad por tipo.
    private readonly List<PowerUpType> owned = new List<PowerUpType>();
    private readonly Dictionary<PowerUpType, int> counts = new Dictionary<PowerUpType, int>();
    // Momento (Time.time) en que cada tipo vuelve a estar disponible.
    private readonly Dictionary<PowerUpType, float> readyAt = new Dictionary<PowerUpType, float>();
    // Duracion total del ultimo cooldown por tipo (para el barrido del HUD).
    private readonly Dictionary<PowerUpType, float> cooldownTotal = new Dictionary<PowerUpType, float>();
    private int currentIndex = -1;

    private PlayerRespawn respawn;
    private PlayerSizeModifier sizeMod;
    private PlayerFacing facing;

    private void Awake()
    {
        respawn = GetComponent<PlayerRespawn>();
        sizeMod = GetComponent<PlayerSizeModifier>();
        if (sizeMod == null) sizeMod = gameObject.AddComponent<PlayerSizeModifier>();
        facing = GetComponentInChildren<PlayerFacing>();
    }

    private void OnEnable()  { SceneManager.activeSceneChanged += OnSceneChanged; }
    private void OnDisable() { SceneManager.activeSceneChanged -= OnSceneChanged; }

    // Al cambiar de nivel: el inventario NO se lleva a la otra escena.
    private void OnSceneChanged(Scene from, Scene to) => ClearAll();

    private void ClearAll()
    {
        owned.Clear();
        counts.Clear();
        readyAt.Clear();
        currentIndex = -1;
        if (sizeMod != null) sizeMod.ResetSize();
        UpdateHUD();
    }

    // -----------------------------------------------------------------
    //   INPUT (mensajes del PlayerInput "Send Messages")
    // -----------------------------------------------------------------
    public void OnUsePowerUp(InputValue value)
    {
        if (!value.isPressed) return;
        var cur = Current;
        if (cur == null) return;            // nada seleccionado
        if (!Has(cur.Value)) return;        // gating: no esta en el inventario
        UsePowerUp(cur.Value);
    }

    public void OnNextPowerUp(InputValue value)
    {
        if (!value.isPressed || owned.Count == 0) return;
        if (currentIndex < 0) currentIndex = 0;
        else currentIndex = (currentIndex + 1) % owned.Count;
        UpdateHUD();
    }

    public void OnPrevPowerUp(InputValue value)
    {
        if (!value.isPressed || owned.Count == 0) return;
        if (currentIndex < 0) currentIndex = 0;
        else currentIndex = (currentIndex - 1 + owned.Count) % owned.Count;
        UpdateHUD();
    }

    // -----------------------------------------------------------------
    //   INVENTARIO
    // -----------------------------------------------------------------
    // Lo llama PowerUpPickup al recoger un power-up en el mapa.
    public void AddPowerUp(PowerUpType type)
    {
        if (!counts.ContainsKey(type))
        {
            counts[type] = 0;
            owned.Add(type);
        }
        counts[type]++;
        if (currentIndex < 0) currentIndex = owned.IndexOf(type);
        UpdateHUD();
    }

    public bool Has(PowerUpType type) => counts.TryGetValue(type, out int c) && c > 0;
    public int CountOf(PowerUpType type) => counts.TryGetValue(type, out int c) ? c : 0;
    public bool HasAny => owned.Count > 0;
    public bool OnCooldown(PowerUpType type) => Time.time < (readyAt.TryGetValue(type, out var v) ? v : 0f);
    public PowerUpType? Current =>
        (currentIndex >= 0 && currentIndex < owned.Count) ? owned[currentIndex] : (PowerUpType?)null;

    // -----------------------------------------------------------------
    //   USO
    // -----------------------------------------------------------------
    private void UsePowerUp(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Cuquis:
                if (OnCooldown(type)) return;          // 1 cada 3s
                FireCuqui();
                StartCooldown(type, cuquiCooldown);
                break;                                  // ilimitado: NO se consume

            case PowerUpType.Heart:
                // Excepcion: se consume. Solo si realmente cura.
                if (respawn != null && respawn.GainLife(1)) Consume(type);
                break;

            case PowerUpType.Chips:
                if (OnCooldown(type) || sizeMod == null) return;
                sizeMod.ApplySmall(chipsDuration);
                StartCooldown(type, chipsDuration + chipsCooldown); // efecto + espera
                break;                                  // reutilizable: NO se consume

            case PowerUpType.Merengue:
                if (OnCooldown(type) || sizeMod == null) return;
                sizeMod.ApplyBig(merengueDuration);
                StartCooldown(type, merengueDuration + merengueCooldown);
                break;                                  // reutilizable: NO se consume
        }
    }

    private void FireCuqui()
    {
        int dir = facing != null ? facing.FacingSign : 1;

        Vector3 from;
        if (cuquiMuzzle != null)
        {
            from = cuquiMuzzle.position;
        }
        else
        {
            // Offset relativo a Pops, escalado con su tamano (para que con
            // Chips/Merengue siga saliendo del cuerpo y no flotando).
            Vector3 off = new Vector3(cuquiMuzzleOffset.x * dir, cuquiMuzzleOffset.y, cuquiMuzzleOffset.z);
            from = transform.position + Vector3.Scale(off, transform.lossyScale);
        }

        CuquiProjectile.Spawn(from, Vector3.right * dir, cuquiSpeed, cuquiProjectilePrefab);
    }

    // Solo para Heart (los demas no se gastan).
    private void Consume(PowerUpType type)
    {
        if (!Has(type)) return;
        counts[type]--;
        if (counts[type] <= 0)
        {
            counts.Remove(type);
            int idx = owned.IndexOf(type);
            if (idx >= 0) owned.RemoveAt(idx);
            currentIndex = owned.Count == 0 ? -1 : Mathf.Clamp(currentIndex, 0, owned.Count - 1);
        }
        UpdateHUD();
    }

    private void StartCooldown(PowerUpType type, float duration)
    {
        readyAt[type] = Time.time + duration;
        cooldownTotal[type] = Mathf.Max(0.01f, duration);
    }

    private void Update()
    {
        // Alimenta el barrido de enfriamiento del HUD del power-up seleccionado.
        var cur = Current;
        float frac = 0f;
        if (cur != null && readyAt.TryGetValue(cur.Value, out float ready))
        {
            float remaining = ready - Time.time;
            if (remaining > 0f && cooldownTotal.TryGetValue(cur.Value, out float total) && total > 0f)
                frac = Mathf.Clamp01(remaining / total);
        }
        HUDController.Instance?.UpdatePowerUpCooldown(frac);
    }

    // -----------------------------------------------------------------
    //   HUD
    // -----------------------------------------------------------------
    private Sprite IconFor(PowerUpType type)
    {
        if (icons != null)
            for (int i = 0; i < icons.Length; i++)
                if (icons[i].type == type) return icons[i].icon;
        return null;
    }

    private void UpdateHUD()
    {
        var cur = Current;
        Sprite s = cur != null ? IconFor(cur.Value) : null;
        HUDController.Instance?.UpdatePowerUp(s);
    }
}
