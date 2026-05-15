using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class PlayButtonManager : MonoBehaviour
{
    public Animator animator;

    public void PlayGame()
    {
        StartCoroutine(PlayAndLoad());
    }

    IEnumerator PlayAndLoad()
    {
        animator.Play("Button", 0, 0f);

        yield return new WaitForSeconds(2f);

        SceneManager.LoadScene("GameScene");
    }
}