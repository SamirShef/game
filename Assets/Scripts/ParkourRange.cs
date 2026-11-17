using System;
using UnityEngine;

[Serializable]
public struct ParkourRange
{
    [SerializeField] private Vector2 range;

    public ParkourRange(float from, float to)
    {
        range = new(from, to);
    }

    public ParkourRange(Vector2 r)
    {
        range = r;
    }

    public static ParkourRange Zero => new(0, 0);
    public readonly float From => range.x;
    public readonly float To => range.y;

    public static bool operator ==(ParkourRange a, ParkourRange b)
    {
        return a.From == b.From && a.To == b.To;
    }

    public static bool operator !=(ParkourRange a, ParkourRange b)
    {
        return a.From != b.From || a.To != b.To;
    }

    public readonly bool IsInto(float s, float tolerance = 0.05f)
    {
        if (Mathf.Approximately(From, To))
        {
            return s >= From - tolerance && s <= From + tolerance;
        }

        return s >= From && s <= To;
    }
}