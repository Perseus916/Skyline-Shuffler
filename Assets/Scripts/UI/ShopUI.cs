using UnityEngine;

public class ShopUI : MonoBehaviour
{
    [Tooltip("Optional: assign the root/panel of the shop UI. If null, this GameObject will be enabled instead.")]
    [SerializeField] private GameObject shopRoot;

    private void Awake()
    {
        // Ensure we start hidden if a root/panel is used.
        if (shopRoot != null)
            shopRoot.SetActive(false);
    }

    public void Show()
    {
        if (shopRoot != null)
        {
            shopRoot.SetActive(true);
            return;
        }

        // Fallback: show this component's GameObject.
        gameObject.SetActive(true);
    }
}

