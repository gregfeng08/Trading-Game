[System.Serializable]
public class DialogueLine
{
    public string speaker;
    [UnityEngine.TextArea(2, 5)]
    public string text;

    public DialogueLine() { }
    public DialogueLine(string speaker, string text)
    {
        this.speaker = speaker;
        this.text = text;
    }
}
