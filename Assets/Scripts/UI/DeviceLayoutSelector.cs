using UnityEngine;

/// <summary>
/// Activates TabletLayout or PhoneLayout depending on the screen aspect ratio.
/// Tablets generally have boxier ratios (e.g., 4:3 or 16:10, ratio <= 1.6), 
/// while phones have longer ratios (e.g., 16:9 or 19.5:9, ratio >= 1.77).
/// </summary>
[ExecuteAlways]
public class DeviceLayoutSelector : MonoBehaviour
{
    [Header("Layout GameObjects")]
    [SerializeField] private GameObject phoneLayout;
    [SerializeField] private GameObject tabletLayout;

    [Header("Detection Threshold")]
    [Tooltip("Aspect ratio = longer side / shorter side. Tablets are usually boxy (iPad is 1.33, standard Android tabs are 1.6). Phones are longer (16:9 is 1.77, iPhone X is 2.16). If the ratio is LESS than this threshold, Tablet Layout is activated. Otherwise, Phone Layout is activated.")]
    [SerializeField] private float aspectThreshold = 1.65f;

    [Header("Editor Debug Options")]
    [SerializeField] private bool forceTablet;
    [SerializeField] private bool forcePhone;

    private int lastWidth = -1;
    private int lastHeight = -1;

    private void Start()
    {
        UpdateLayout();
    }

    private void Update()
    {
        // Monitor screen size changes in editor and runtime (e.g. orientation changes or window resizes)
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            UpdateLayout();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensures layout updates when parameters change in Inspector
        UpdateLayout();
    }
#endif

    public void UpdateLayout()
    {
        if (phoneLayout == null && tabletLayout == null)
        {
            return;
        }

        bool isTablet = CheckIsTablet();

        if (forceTablet) isTablet = true;
        if (forcePhone) isTablet = false;

        if (phoneLayout != null)
        {
            phoneLayout.SetActive(!isTablet);
        }

        if (tabletLayout != null)
        {
            tabletLayout.SetActive(isTablet);
        }
    }

    private bool CheckIsTablet()
    {
        float width = Screen.width;
        float height = Screen.height;

        if (width <= 0 || height <= 0) return false;

        float longerSide = Mathf.Max(width, height);
        float shorterSide = Mathf.Min(width, height);
        
        // longerSide / shorterSide gives orientation-agnostic ratio (e.g. 1.77 for both portrait and landscape 16:9)
        float aspectRatio = longerSide / shorterSide;

        return aspectRatio < aspectThreshold;
    }
}
