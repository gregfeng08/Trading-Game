using UnityEngine;
using System.Threading.Tasks;
using Game.API;
using Game.API.DTO;

public class NPCInteraction : MonoBehaviour
{
    private string npcType;
    private string npcDisplayName;
    private InteractionZone zone;
    private SphereCollider interactionTrigger;

    private const float InteractionRadius = 2.5f;

    public void Init(string type, string displayName)
    {
        npcType = type;
        npcDisplayName = displayName ?? type;

        interactionTrigger = gameObject.AddComponent<SphereCollider>();
        interactionTrigger.isTrigger = true;
        interactionTrigger.radius = InteractionRadius;
        interactionTrigger.center = new Vector3(0f, 1f, 0f);

        zone = gameObject.AddComponent<InteractionZone>();
        zone.Configure(InteractionType.UI, $"Talk to {npcDisplayName}", OnInteract);
    }

    private void OnInteract()
    {
        if (NPCDialogueUI.Inst != null)
            NPCDialogueUI.Inst.Open(npcType, npcDisplayName);
    }
}
