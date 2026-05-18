using UnityEngine;

public class VerticalKillerPlatform : MonoBehaviour
{
    [Header("Ajustes")]
    public float riseSpeed = 10f;       // Velocidad de subida rápida
    public float delayBeforeRise = 0.2f; // Tiempo antes de empezar a subir
    public LayerMask playerLayer;        // Layer del jugador

    private bool playerTouched = false;
    private bool rising = false;
    private float timer = 0f;

    private Vector3 startPos;

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        // Si el jugador tocó la plataforma pero aún no empieza a subir
        if (playerTouched && !rising)
        {
            timer += Time.deltaTime;
            if (timer >= delayBeforeRise)
            {
                rising = true;
            }
        }

        // Mover plataforma hacia arriba
        if (rising)
        {
            transform.position += Vector3.up * riseSpeed * Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playerTouched) return; // evitar activar dos veces

        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerTouched = true;
        }
    }
}
