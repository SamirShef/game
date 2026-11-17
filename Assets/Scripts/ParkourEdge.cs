using System;
using UnityEngine;

[Serializable]
public class ParkourEdge
{
    [SerializeField] private ParkourNode targetNode = null;
    [SerializeField] private ParkourRange from = ParkourRange.Zero;
    [SerializeField] private ParkourRange to = ParkourRange.Zero;

    public ParkourNode TargetNode => targetNode;
    public ParkourRange FromRange => from;
    public ParkourRange ToRange => to;

    public Vector3 GetDirection(Vector3 currentPos)
    {
        if (TargetNode == null)
        {
            return Vector3.zero;
        }
        return (TargetNode.PointAt((to.To + to.From) / 2) - currentPos).normalized;
    }
}