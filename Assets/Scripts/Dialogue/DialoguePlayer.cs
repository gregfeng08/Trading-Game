using UnityEngine;

public class DialoguePlayer : MonoBehaviour
{
    public static DialoguePlayer Inst { get; private set; }

    private SpeechBubble activeBubble;
    private DialogueLine[] lines;
    private int currentIndex;
    private System.Action onComplete;

    public bool IsPlaying { get; private set; }

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (!IsPlaying) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
        {
            if (activeBubble.IsTyping)
                activeBubble.SkipTypewriter();
            else
                Advance();
        }
    }

    public void Play(DialogueLine[] dialogue, SpeechBubble bubble, System.Action callback = null)
    {
        lines = dialogue;
        activeBubble = bubble;
        onComplete = callback;
        currentIndex = 0;
        IsPlaying = true;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);

        ShowLine(0);
    }

    private void Advance()
    {
        currentIndex++;
        if (currentIndex >= lines.Length)
        {
            End();
            return;
        }
        ShowLine(currentIndex);
    }

    private void ShowLine(int index)
    {
        var line = lines[index];
        activeBubble.Show(line.speaker, line.text, SpeakerColor(line.speaker));
    }

    public void ForceEnd()
    {
        if (!IsPlaying) return;
        End();
    }

    private void End()
    {
        activeBubble.Hide();
        activeBubble = null;
        lines = null;
        IsPlaying = false;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);

        onComplete?.Invoke();
        onComplete = null;
    }

    private static Color SpeakerColor(string speaker)
    {
        return speaker switch
        {
            "Casey" => new Color(0.4f, 0.85f, 0.7f),
            "The Principal" => new Color(0.85f, 0.75f, 0.45f),
            _ => Color.white,
        };
    }
}
