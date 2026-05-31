using System.Collections.Generic;
using UnityEngine;


public class BranchNode
{
    public Vector3 Position;

    public Quaternion Orientation;

    public float Radius;

    public int Depth;

    public BranchNode Parent;

    public List<BranchNode> Children = new List<BranchNode>();

    public int Index;

    public float Vigor = 1f;

    public BranchNode(Vector3 position, Quaternion orientation, float radius, int depth, BranchNode parent = null)
    {
        Position = position;
        Orientation = orientation;
        Radius = radius;
        Depth = depth;
        Parent = parent;

        if (parent != null)
        {
            parent.Children.Add(this);
        }
    }

    public bool IsLeaf => Children.Count == 0;

    public bool IsBranching => Children.Count > 1;

    public bool IsRoot => Parent == null;
}

public struct BranchSegment
{
    public Vector3 Start;
    public Vector3 End;
    public float StartRadius;
    public float EndRadius;
    public int Depth;
    public BranchNode StartNode;
    public BranchNode EndNode;

    public Vector3 Direction => (End - Start).normalized;

    public float Length => Vector3.Distance(Start, End);
}
