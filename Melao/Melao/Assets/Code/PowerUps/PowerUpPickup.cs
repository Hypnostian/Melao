using UnityEngine;

// Objeto de power-up colocado en el mapa. Gira (y opcionalmente flota) para
// llamar la atencion, y al tocarlo Pops lo GUARDA en su inventario
// (PowerUpController) listo para usar. Luego se destruye.
//
// El collider de este objeto debe ser TRIGGER (lo configura PowerUpSetupTool).
[DisallowMultipleComponent]
public class PowerUpPickup : MonoBehaviour
{
    [Header("Tipo de power-up")]
    public PowerUpType type = PowerUpType.Cuquis;

    [Header("Rotacion")]
    public Vector3 rotationAxis = Vector3.up;
    [Tooltip("Grados por segundo.")]
    public float rotationSpeed = 90f;

    [Header("Flotacion")]
    public bool bob = true;
    public float bobAmplitude = 0.15f;
    public float bobSpeed = 2f;

    [Header("Recoleccion")]
    public AudioClip pickupSound;
    public LayerMask playerLayer = 1 << 7;

    private Vector3 startPos;
    private bool collected;

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        if (rotationSpeed != 0f)
            transform.Rotate(rotationAxis.normalized * rotationSpeed * Time.deltaTime, Space.World);

        if (bob)
        {
            Vector3 p = transform.position;
            p.y = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = p;
        }
    }

    private void OnTriggerEnter(Collider other) { TryCollect(other); }

    private void TryCollect(Collider other)
    {
        if (collected) return;

        bool isPlayer = ((1 << other.gameObject.layer) & playerLayer) != 0
                        || other.transform.root.CompareTag("Player");
        if (!isPlayer) return;

        var ctrl = other.GetComponentInParent<PowerUpController>();
        if (ctrl == null) ctrl = other.transform.root.GetComponentInChildren<PowerUpController>();
        if (ctrl == null) return;

        collected = true;
        ctrl.AddPowerUp(type);

        if (pickupSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(pickupSound);

        Destroy(gameObject);
    }
}
