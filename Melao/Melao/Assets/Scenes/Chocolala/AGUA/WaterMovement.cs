using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WaterMovement : MonoBehaviour
{
    [Header("Configuración de Olas Principales")]
    [Tooltip("Altura de las olas")]
    public float waveAmplitude = 0.2f;

    [Tooltip("Qué tan juntas están las olas")]
    public float waveFrequency = 1f;

    [Tooltip("Velocidad del movimiento")]
    public float waveSpeed = 1f;

    [Tooltip("Dirección del movimiento en el plano XZ")]
    public Vector2 waveDirection = new Vector2(1f, 0.5f);

    [Header("Olas Secundarias (más realismo)")]
    public bool useSecondaryWaves = true;
    public float secondaryAmplitude = 0.1f;
    public float secondaryFrequency = 2f;
    public float secondarySpeed = 1.5f;
    public Vector2 secondaryDirection = new Vector2(-0.5f, 1f);

    private Mesh waterMesh;
    private Vector3[] originalVertices;
    private Vector3[] displacedVertices;

    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        // Clonamos el mesh para no modificar el asset original
        waterMesh = Instantiate(meshFilter.sharedMesh);
        meshFilter.mesh = waterMesh;

        // Guardamos los vértices originales como referencia
        originalVertices = waterMesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
    }

    void Update()
    {
        Vector2 dir1 = waveDirection.normalized;
        Vector2 dir2 = secondaryDirection.normalized;

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 vertex = originalVertices[i];

            // Ola principal
            float wave1 = Mathf.Sin(
                (vertex.x * dir1.x + vertex.z * dir1.y) * waveFrequency
                + Time.time * waveSpeed
            ) * waveAmplitude;

            float totalDisplacement = wave1;

            // Ola secundaria (cruzada para un patrón más natural)
            if (useSecondaryWaves)
            {
                float wave2 = Mathf.Sin(
                    (vertex.x * dir2.x + vertex.z * dir2.y) * secondaryFrequency
                    + Time.time * secondarySpeed
                ) * secondaryAmplitude;

                totalDisplacement += wave2;
            }

            // Aplicamos el desplazamiento en el eje Y
            displacedVertices[i] = new Vector3(
                vertex.x,
                vertex.y + totalDisplacement,
                vertex.z
            );
        }

        waterMesh.vertices = displacedVertices;
        waterMesh.RecalculateNormals();
        waterMesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (waterMesh != null)
            Destroy(waterMesh);
    }
}