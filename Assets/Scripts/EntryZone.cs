using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class EntryZone : MonoBehaviour
{
    [SerializeField] private string destinationSceneName;
    [SerializeField] private GameObject loadingCanvas;

    private bool isLoading = false;

    void OnTriggerEnter(Collider other)
    {
        if (isLoading) return;
        if (!other.CompareTag("Player")) return;

        StartCoroutine(LoadScene());
    }

    IEnumerator LoadScene()
    {
        isLoading = true;

        loadingCanvas.SetActive(true);

        AsyncOperation op = SceneManager.LoadSceneAsync(destinationSceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            // update progress bar here
            yield return null;
        }

        yield return new WaitForSeconds(0.5f); // polish pause
        op.allowSceneActivation = true;
    }
}
