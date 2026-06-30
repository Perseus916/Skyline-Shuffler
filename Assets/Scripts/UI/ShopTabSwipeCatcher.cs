using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this to an invisible Image (Raycast Target = true) that covers
/// the shop content area.  It forwards drag events to ShopTabSwitcher
/// so swiping on the content area switches tabs.
///
/// Setup:
///   1. Create a child GameObject under your tab content container.
///   2. Add an Image component, set its alpha to 0 (invisible) but keep Raycast Target ON.
///   3. Stretch it to fill the content area.
///   4. Add this component and drag the ShopTabSwitcher reference.
/// </summary>
public class ShopTabSwipeCatcher : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Reference to the ShopTabSwitcher that handles tab switching.")]
    [SerializeField] private ShopTabSwitcher tabSwitcher;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (tabSwitcher != null)
            tabSwitcher.OnSwipeBegin(eventData.position.x);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Intentionally empty – we only need begin/end for swipe detection.
        // This must be implemented for IEndDragHandler to receive events.
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (tabSwitcher != null)
            tabSwitcher.OnSwipeEnd(eventData.position.x);
    }
}
