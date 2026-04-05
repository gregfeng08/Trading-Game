using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class InteractionZone : MonoBehaviour
{
    [SerializeField] private string interactionName;
    [SerializeField] private UnityEvent onInteract;
    [SerializeField] private GameObject promptInteract;

    [SerializeField] private Vector3 worldOffset = new Vector3(0, -0.5f, 0);

    private bool playerInZone;
    private RectTransform promptRect;
    private Camera mainCam;

    void Start()
    {
        promptInteract.SetActive(false);
        promptRect = promptInteract.GetComponent<RectTransform>();
        mainCam = Camera.main;
    }

    void Update()
    {
        if (playerInZone)
        {
            Vector3 screenPos = mainCam.WorldToScreenPoint(transform.position + worldOffset);
            promptRect.position = screenPos;

            if (Input.GetKeyDown(KeyCode.E))
                onInteract.Invoke();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            playerInZone = true;
            promptInteract.SetActive(true);
            var tmp = promptInteract.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
                tmp.text = $"Press E to {interactionName}";
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            playerInZone = false;
            promptInteract.SetActive(false);
        }
    }
}
