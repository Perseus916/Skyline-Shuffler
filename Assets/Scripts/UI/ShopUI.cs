using UnityEngine;

public class ShopUI : MonoBehaviour
{
    [Tooltip("Optional: assign the root/panel of the shop UI. If null, this GameObject will be enabled instead.")]
    [SerializeField] private GameObject shopRoot;

    private void Awake()
    {
        // Ensure we start hidden if a root/panel is used, but only at the very start of the game.
        // If Awake is called later (e.g., when the panel is first activated), do not hide it.
        if (Time.frameCount == 0 && shopRoot != null)
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

