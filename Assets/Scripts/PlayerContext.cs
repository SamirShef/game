using UnityEngine;

public enum PlayerState
{
    Idle,
    Locomotion,
    Jumping,
    Balancing,

    ParkourTransition,
    Hanging,
    Climbing,
    Shimmy
}

public class PlayerContext : MonoBehaviour
{
    public PlayerState State = PlayerState.Idle;
}