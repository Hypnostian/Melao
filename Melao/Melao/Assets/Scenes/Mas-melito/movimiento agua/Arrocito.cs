using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class Arrocito : MonoBehaviour
{
    [Header("Olas Principales")]
    public float amplitude = 0.2f;
    public float frequency = 1f;
    public float speed = 1f;
    public Vector2 direction = new Vector2(1f, 0.5f);

    [Header("Olas Secundarias")]
    public bool enableSecondary = true;
    public float secAmplitude = 0.1f;
    public float secFrequency = 2f;
    public float secSpeed = 1.5f;
    public Vector2 secDirection = new Vector2(-0.5f, 1f);

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] movedVertices;

    void Start()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        mesh = Instantiate(mf.sharedMesh);
        mf.mesh = mesh;

        baseVertices = mesh.vertices;
        movedVertices = new Vector3[baseVertices.Length];
    }

    void Update()
    {
        Vector2 d1 = direction.normalized;
        Vector2 d2 = secDirection.normalized;
        float t = Time.time;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 v = baseVertices[i];

            float y = Mathf.Sin((v.x * d1.x + v.z * d1.y) * frequency + t * speed) * amplitude;

            if (enableSecondary)
                y += Mathf.Sin((v.x * d2.x + v.z * d2.y) * secFrequency + t * secSpeed) * secAmplitude;

            movedVertices[i] = new Vector3(v.x, v.y + y, v.z);
        }

        mesh.vertices = movedVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }
}