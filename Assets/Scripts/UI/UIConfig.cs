using UnityEngine;

[CreateAssetMenu(menuName = "Game/UI Config")]
public class UIConfig : ScriptableObject
{
    public static UIConfig Inst { get; private set; }

    [Header("Knowledge Graph")]
    public float graphNodeTitleSize = 14f;
    public float graphDetailTitleSize = 22f;
    public float graphDetailContentSize = 16f;
    public float graphDetailStatusSize = 13f;

    [Header("Casey Dialogue")]
    public float caseyNameSize = 18f;
    public float caseyBodySize = 15f;
    public float caseyPromptSize = 13f;
    public float caseyButtonSize = 16f;

    [Header("Trading UI")]
    public float tradingStatusSize = 14f;
    public float tradingTickerSize = 16f;

    [Header("HUD")]
    public float hudDateSize = 14f;
    public float hudArcSize = 13f;

    [Header("Daily Summary")]
    public float summaryHeaderSize = 18f;
    public float summaryBodySize = 14f;

    [Header("Global Scale")]
    [Range(0.5f, 2f)]
    public float globalTextScale = 1f;

    public float Scale(float baseSize) => baseSize * globalTextScale;

    public void Register() => Inst = this;
}
