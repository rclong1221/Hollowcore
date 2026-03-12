#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

[ExecuteAlways]
public class ClimbDebugVisualizer : MonoBehaviour
{
    [Header("Ray Settings")]
    public Transform playerTransform;
    public float eyeHeight = 1.6f;
    public float maxRange = 2.0f;
    public float aimTolerance = 0.5f;
    public Color rayColor = Color.green;
    public Color hitColor = Color.red;

    [Header("Debug")]
    public bool drawEveryFrame = true;
    public Vector3 lastHitPoint = Vector3.zero;
    public bool lastHit = false;

    void Update()
    {
        if (!Application.isPlaying && !drawEveryFrame)
            return;

        if (playerTransform == null)
            playerTransform = transform;

        Vector3 origin = playerTransform.position + Vector3.up * eyeHeight;
        Vector3 dir = playerTransform.forward;

        lastHit = Physics.Raycast(origin, dir, out RaycastHit hit, maxRange);
        if (lastHit)
        {
            lastHitPoint = hit.point;
        }
    }

    void OnDrawGizmos()
    {
        if (playerTransform == null)
            playerTransform = transform;

        Vector3 origin = playerTransform.position + Vector3.up * eyeHeight;
        Vector3 dir = playerTransform.forward;

        Gizmos.color = rayColor;
        Gizmos.DrawLine(origin, origin + dir * maxRange);
        Gizmos.DrawWireSphere(origin + dir * maxRange, 0.03f);

        if (lastHit)
        {
            Gizmos.color = hitColor;
            Gizmos.DrawSphere(lastHitPoint, 0.05f);
        }
    }
}
#endif
