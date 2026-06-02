using UnityEngine;

// Hazard universal: cualquier objeto con este script mata al jugador al contacto.
// Reutilizable para spikes, lava, gol, saltin, etc.
// Funciona tanto con collider solido (OnCollisionEnter) como con trigger (OnTriggerEnter).
[DisallowMultipleComponent]
public class HazardKill : MonoBehaviour
{
    [Header("Filtro de jugador")]
    [Tooltip("Layers que cuentan como jugador. Por defecto: solo el layer 'Player'.")]
    public LayerMask playerLayer = 1 << 7;

    [Tooltip("Si esta activado, tambien acepta objetos con tag 'Player' (compatibilidad con prefabs antiguos).")]
    public bool alsoUseTag = true;

    [Header("Direccionalidad")]
    [Tooltip("Si esta activado, SOLO mata cuando el jugador toca por ARRIBA (aterriza encima). Util para trampas tipo MilHojas(5). Requiere collider solido (no trigger).")]
    public bool onlyKillFromTop = false;

    [Header("Comportamiento")]
    [Tooltip("Si esta activo (lava, caida, gol): MATA y respawnea en el checkpoint. " +
             "Si NO (espinas/espaditas): solo resta 1 corazon, empuja hacia atras y " +
             "da invulnerabilidad breve; el jugador sigue jugando donde esta.")]
    public bool lethal = true;

    [Tooltip("Pequeno cooldown para evitar matar dos veces al mismo player en el mismo frame.")]
    public float retriggerCooldown = 0.25f;

    private float lastKillTime = -999f;

    // Se comprueba en Enter Y en Stay. El Stay es CLAVE: garantiza la muerte aunque
    // el solapamiento no venga del movimiento normal, sino de OTRO motivo: Pops
    // creciendo con Merengue / encogiendo con Chips DENTRO del hazard, o quedando
    // ya solapada. Asi la muerte se verifica en CUALQUIER estado de Pops, no solo
    // en el frame de entrada. retriggerCooldown evita golpear dos veces seguidas.
    private void OnTriggerEnter(Collider other)
    {
        if (onlyKillFromTop) return;
        TryKill(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (onlyKillFromTop) return;
        TryKill(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (onlyKillFromTop && !ContactIsFromTop(collision)) return;
        TryKill(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (onlyKillFromTop && !ContactIsFromTop(collision)) return;
        TryKill(collision.collider);
    }

    private void TryKill(Collider other)
    {
        if (Time.time - lastKillTime < retriggerCooldown) return;
        if (!IsPlayer(other)) return;

        PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null) return;

        lastKillTime = Time.time;
        if (lethal) respawn.Kill();
        else respawn.Damage(transform.position);   // espinas: 1 corazon + empuje
    }

    // El jugador toca "por arriba" si algun punto de contacto esta en la mitad
    // superior del collider de este hazard. Robusto frente a la convencion de
    // normales (que varia entre versiones de Unity).
    private bool ContactIsFromTop(Collision collision)
    {
        Collider myCol = GetComponent<Collider>();
        if (myCol == null) myCol = GetComponentInChildren<Collider>();
        if (myCol == null) return true;

        float centerY = myCol.bounds.center.y;
        int n = collision.contactCount;
        for (int i = 0; i < n; i++)
        {
            if (collision.GetContact(i).point.y >= centerY) return true;
        }
        return false;
    }

    private bool IsPlayer(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0) return true;
        if (alsoUseTag && other.transform.root.CompareTag("Player")) return true;
        return false;
    }
}
