using UnityEngine;

public class CraneBuilder : MonoBehaviour
{
    [Header("Tower")]

    public Transform stand;

    public Transform Cranepillar;

    public int towerCount = 5;

    public float pillarHeight = 0.028f;

    [Header("Cabin")]

    public Transform cabinBase;

    public Transform cabin;
    public float cabinOffset = 0f;

    [Header("Horizontal")]

    public Transform horizontalCrane;

    public Transform trolley;

    

    [Header("Rope")]

    public Transform ropeTemplate;

    public Transform ropeParent;

    public int ropeCount = 5;

    public float ropeSpacing = 0.5f;

    [Header("Hook")]

    public Transform hook;

    void Start()
    {
        GenerateTower();

        GenerateRope();
    }


    void GenerateTower()
    {
        for (int i = 1; i < towerCount; i++)
        {
            Transform newBlock =
                Instantiate(
                    Cranepillar,
                    stand
                );

            // Ensure the block has no rotation and the desired scale (x=1, y=1, z=1)
            newBlock.localRotation = Quaternion.identity;
            newBlock.localScale = new Vector3(1f, 1f, 1f);

            // Position blocks vertically relative to the stand and set Z position to 0.014
            newBlock.localPosition =
                new Vector3(
                    0f,
                    0f,
                    i * pillarHeight
                );
        }

        float totalHeight =
            towerCount * pillarHeight;

        Vector3 pos =
            stand.localPosition;

        pos.z =
            totalHeight -0.083f;

        cabinBase.localPosition = pos;
        pos.z =pos.z-0.001f;
        horizontalCrane.localPosition = pos;
    }

    void GenerateRope()
    {
       // ropeTemplate.gameObject.SetActive(false);

        for (int i = 0; i < ropeCount; i++)
        {
            Transform newRope =
                Instantiate(
                    ropeTemplate,
                    ropeParent
                );

            newRope.gameObject.SetActive(true);

            newRope.localPosition =
                new Vector3(
                    0,
                    -i * ropeSpacing,
                    0
                );
        }

        hook.localPosition =
            new Vector3(
                0,
                -(ropeCount * ropeSpacing-1.75f),
                0
            );
    }

}