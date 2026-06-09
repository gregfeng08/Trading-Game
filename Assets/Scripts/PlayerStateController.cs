using System;
using UnityEngine;

public enum PlayerState
{
    MOVING,
    TRADING,
    CUTSCENE,
    PAUSED
}

public class PlayerStateController : MonoBehaviour
{
    public static PlayerStateController Inst { get; private set; }

    public PlayerState State { get; private set; } = PlayerState.MOVING;

    /// <summary>Fires after every state change. Args: (oldState, newState).</summary>
    public event Action<PlayerState, PlayerState> OnStateChanged;

    private Action activeUIClose;
    private PlayerState activeUIState;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            switch (State)
            {
                case PlayerState.PAUSED:   break;
                case PlayerState.TRADING:  SetState(PlayerState.MOVING); break;
                case PlayerState.MOVING:   break;
            }
        }
    }

    public void OpenUI(PlayerState state, Action onClose)
    {
        if (activeUIClose != null)
        {
            var prev = activeUIClose;
            activeUIClose = null;
            prev.Invoke();
        }

        activeUIClose = onClose;
        activeUIState = state;
        SetState(state);
    }

    public void SetState(PlayerState newState)
    {
        if (newState == State) return;

        PlayerState old = State;
        State = newState;

        if (activeUIClose != null && old == activeUIState)
        {
            var cb = activeUIClose;
            activeUIClose = null;
            cb.Invoke();
        }

        ApplyCursor(newState);
        OnStateChanged?.Invoke(old, newState);
    }

    void ApplyCursor(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.MOVING:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;

            case PlayerState.TRADING:
            case PlayerState.CUTSCENE:
            case PlayerState.PAUSED:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;
        }
    }

    /// <summary>True when the player character should be able to move.</summary>
    public bool CanMove => State == PlayerState.MOVING;

    /// <summary>True when the player can interact with objects.</summary>
    public bool CanInteract => State == PlayerState.MOVING;
}
