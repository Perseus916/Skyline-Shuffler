using UnityEngine;

public class CloudFaceCamera : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main == null)
            return;

        Vector3 direction = Camera.main.transform.position - transform.position;

        direction.y = 0f;

        Quaternion rotation = Quaternion.LookRotation(-direction);

        transform.rotation = rotation * Quaternion.Euler(0f, -45f, 0f);
    }
}