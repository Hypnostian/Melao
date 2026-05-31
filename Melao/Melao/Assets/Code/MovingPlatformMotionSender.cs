using UnityEngine;

public class MovingPlatformMotionSender : MonoBehaviour
{
    private Vector3 lastPos;
    public Vector3 platformVelocity { get; private set; }

    void Start()
    {
        lastPos = transform.position;
    }

    void LateUpdate()
    {
        platformVelocity = (transform.position - lastPos) / Time.deltaTime;
        lastPos = transform.position;
    }
}
