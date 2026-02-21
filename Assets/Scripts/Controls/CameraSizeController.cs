using UnityEngine;

[ExecuteInEditMode]
public class CameraSizeController : MonoBehaviour
{
    [SerializeField] private float sceneWidth = 15f; // Total width of your 5x5 grid in Unity units
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        // Calculate the required ortho size based on screen aspect ratio
        // unitsPerPixel = orthoSize * 2 / screenHeight
        // targetWidth = unitsPerPixel * screenWidth

        float unitsPerPixel = sceneWidth / Screen.width;
        float desiredHalfHeight = 0.5f * unitsPerPixel * Screen.height;

        cam.orthographicSize = desiredHalfHeight;
    }
}