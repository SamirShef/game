using System.Collections.Generic;
using UnityEngine;

public enum ParkourNodeType
{
    Ledge,
    Beam,
}

public class ParkourNode : MonoBehaviour
{
    public static readonly string LayerName = "ParkourNode";

    [SerializeField] private ParkourNodeType nodeType;
    [SerializeField] private Vector3 normalVector = -Vector3.forward;
    [SerializeField] private Vector3 rightVector = Vector3.right;
    [SerializeField] private float length = 0;
    [SerializeField] private List<ParkourEdge> outgoingEdges = new();

    public ParkourNodeType NodeType => nodeType;
    public List<ParkourEdge> OutgoingEdges => outgoingEdges;

    public Vector3 Normal => normalVector;
    public Vector3 GlobalNormal => transform.TransformDirection(Normal);
    public Vector3 Right => rightVector;
    public Vector3 GlobalRight => transform.TransformDirection(Right);
    public float Length => length;
    public Vector3 Start => transform.position - Length / 2 * GlobalRight; // Left point
    public Vector3 End => transform.position + Length / 2 * GlobalRight;   // Right point

    private void Awake()
    {
        int layerIndex = LayerMask.NameToLayer(LayerName);
        if (layerIndex != -1)
        {
            gameObject.layer = layerIndex;
        }
        else
        {
            Debug.LogError("Layer 'ParkourNode' not found");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = NodeType switch
        {
            ParkourNodeType.Ledge => Color.green,
            ParkourNodeType.Beam => Color.cyan
        };

        Gizmos.DrawWireSphere(transform.position, 0.2f);
        if (Length != 0)
        {
            Gizmos.DrawRay(transform.position, Length / 2 * GlobalRight);
            Gizmos.DrawRay(transform.position, -Length / 2 * GlobalRight);
        }

        foreach (var edge in OutgoingEdges)
        {
            if (edge.TargetNode != null)
            {
                var t = edge.TargetNode;
                Color col = Color.yellow;
                Gizmos.color = col;
                Gizmos.DrawLine(PointAt((edge.FromRange.From + edge.FromRange.To) / 2), t.PointAt((edge.ToRange.From + edge.ToRange.To) / 2));
                if (edge.FromRange != ParkourRange.Zero)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(PointAt(edge.FromRange.From), PointAt(edge.FromRange.To));
                }
                if (edge.ToRange != ParkourRange.Zero)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(t.PointAt(edge.ToRange.From), t.PointAt(edge.ToRange.To));
                }
            }
        }
    }

    public Vector3 PointAt(float s) => Start + GlobalRight * s;
    public float Project(Vector3 p) => Mathf.Clamp(Vector3.Dot(p - Start, GlobalRight), 0, Length);
    public Vector3 PointAtProject(Vector3 p) => PointAt(Project(p));
}