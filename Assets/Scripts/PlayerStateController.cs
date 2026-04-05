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
                case PlayerState.MOVING:   SetState(PlayerState.PAUSED); break;
                case PlayerState.PAUSED:   SetState(PlayerState.MOVING); break;
                case PlayerState.TRADING:  SetState(PlayerState.MOVING); break;
            }
        }
    }

    public void SetState(PlayerState newState)
    {
        if (newState == State) return;

        PlayerState old = State;
        State = newState;

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
