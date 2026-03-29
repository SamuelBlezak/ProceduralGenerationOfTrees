using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uzol vetviacej struktury stromu (skeletalny graf).
/// Kazdy uzol ma poziciu, orientaciu, hrubku a referencie na rodicov a potomkov.
/// Toto je zakladna datova struktura na ktoru sa neskor napoja strandy.
/// </summary>
public class BranchNode
{
    /// <summary>Pozicia uzla vo world space.</summary>
    public Vector3 Position;

    /// <summary>Orientacia uzla (smer rastu).</summary>
    public Quaternion Orientation;

    /// <summary>Polomer vetvy v tomto uzle.</summary>
    public float Radius;

    /// <summary>Hlbka vetvenia (0 = kmen, 1 = hlavne vetvy, atd.).</summary>
    public int Depth;

    /// <summary>Rodicovsky uzol (null pre koren).</summary>
    public BranchNode Parent;

    /// <summary>Potomkovia (deti) tohto uzla.</summary>
    public List<BranchNode> Children = new List<BranchNode>();

    /// <summary>Index v globalnom zozname uzlov (pouziva sa pri mesh generovani).</summary>
    public int Index;

    /// <summary>Vigor hodnota pre interaktivne rastenie (pouzije sa v neskorsi fazach).</summary>
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

    /// <summary>Je toto koncovy uzol (list)?</summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>Je toto vetvenie (bifurkacia)?</summary>
    public bool IsBranching => Children.Count > 1;

    /// <summary>Je toto korenovy uzol?</summary>
    public bool IsRoot => Parent == null;
}

/// <summary>
/// Segment vetvy medzi dvoma uzlami. Pouziva sa pri mesh generovani.
/// </summary>
public struct BranchSegment
{
    public Vector3 Start;
    public Vector3 End;
    public float StartRadius;
    public float EndRadius;
    public int Depth;
    public BranchNode StartNode;
    public BranchNode EndNode;

    /// <summary>Smer segmentu (normalizovany).</summary>
    public Vector3 Direction => (End - Start).normalized;

    /// <summary>Dlzka segmentu.</summary>
    public float Length => Vector3.Distance(Start, End);
}
