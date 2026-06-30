using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this to ANY UI element to enable swipe-to-switch between shop tabs.
/// Works on invisible overlays, content panels (Undo/Hint), or any raycast target.
///
/// How it works:
///   - Horizontal swipes are forwarded to ShopTabSwitcher to switch tabs.
///   - Small movements (taps) are ignored so buttons underneath still receive clicks.
///   - Vertical scrolls are ignored so ScrollRects still work if present.
///
/// Setup options (use one or more):
///   A) Attach to an invisible overlay Image covering the content area.
///   B) Attach directly to the Undo content panel (needs Image with Raycast Target ON).
///   C) Attach directly to the Hint content panel (needs Image with Raycast Target ON).
///   All instances must reference the same ShopTabSwitcher.
/// </summary>
public class ShopTabSwipeCatcher : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Reference to the ShopTabSwitcher that handles tab switching.")]
    [SerializeField] private ShopTabSwitcher tabSwitcher;

    [Tooltip("Minimum horizontal distance (pixels) to count as a swipe vs a tap.")]
    [SerializeField] private float dragDeadzone = 10f;

    private bool isSwiping;
    private Vector2 pointerDownPos;

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPos = eventData.position;
        isSwiping = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Only treat as swipe if horizontal movement exceeds vertical
        float dx = Mathf.Abs(eventData.position.x - pointerDownPos.x);
        float dy = Mathf.Abs(eventData.position.y - pointerDownPos.y);

        if (dx > dy && dx > dragDeadzone)
        {
            isSwiping = true;
            if (tabSwitcher != null)
                tabSwitcher.OnSwipeBegin(pointerDownPos.x);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // If we haven't started swiping yet, check again
        if (!isSwiping)
        {
            float dx = Mathf.Abs(eventData.position.x - pointerDownPos.x);
            float dy = Mathf.Abs(eventData.position.y - pointerDownPos.y);

            if (dx > dy && dx > dragDeadzone)
            {
                isSwiping = true;
                if (tabSwitcher != null)
                    tabSwitcher.OnSwipeBegin(pointerDownPos.x);
            }
        }

        // Pass live drag position for real-time panel follow
        if (isSwiping && tabSwitcher != null)
            tabSwitcher.OnSwipeDrag(eventData.position.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isSwiping && tabSwitcher != null)
            tabSwitcher.OnSwipeEnd(eventData.position.x);

        isSwiping = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isSwiping = false;
    }
}
