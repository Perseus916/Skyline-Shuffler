using UnityEngine;

public class ButtonClickAnimation : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void PlayAnimation()
    {
        animator.Play("Button", 0, 0f);
    }
}