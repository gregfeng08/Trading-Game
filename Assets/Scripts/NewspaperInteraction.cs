using UnityEngine;

public class NewspaperInteraction : MonoBehaviour
{
    public void OpenNewspaper()
    {
        if (NewspaperUI.Inst != null)
            NewspaperUI.Inst.Open();
    }
}
