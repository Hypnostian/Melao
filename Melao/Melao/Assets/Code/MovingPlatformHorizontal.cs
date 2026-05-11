using UnityEngine;

public class MovingPlatformHorizontal : MonoBehaviour
{
    [Header("Movimiento")]
    public float amplitude = 2f;  // Qué tan lejos se mueve
    public float speed;      // Velocidad 

    [Header("Offset inicial")]
    public float startOffset = 0f; // Para desincronizar las plataformas

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float x = Mathf.Sin((Time.time + startOffset) * speed) * amplitude;
        transform.position = startPos + new Vector3(x, 0f, 0f);
    }
}
