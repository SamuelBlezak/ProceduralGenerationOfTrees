using System.Collections.Generic;
using UnityEngine;

public class TurtleInterpreter
{
    public float Angle = 25f;
    public float StepLength = 1f;
    public float InitialRadius = 0.5f;
    public float LengthDecay = 0.85f;
    public float AngleVariation = 5f;
    public float DivergenceAngle = 137.5f;

    public float Gravitropism = 0.08f;
    public float Phototropism = 0.05f;

    public int Seed = 42;

    private struct TurtleState
    {
        public Vector3 Position;
        public Quaternion Orientation;
        public float CurrentLength;
        public int Depth;
        public BranchNode CurrentNode;
    }

    public (BranchNode root, List<BranchSegment> segments) Interpret(string lSystemString)
    {
        System.Random rng = new System.Random(Seed);
        BranchNode root = new BranchNode(Vector3.zero, Quaternion.identity, InitialRadius, 0);
        root.Index = 0;

        TurtleState state = new TurtleState
        {
            Position = Vector3.zero,
            Orientation = Quaternion.identity,
            CurrentLength = StepLength,
            Depth = 0,
            CurrentNode = root
        };

        Stack<TurtleState> stateStack = new Stack<TurtleState>();
        List<BranchSegment> segments = new List<BranchSegment>();
        int nodeIndex = 1;
        int branchCounter = 0;

        foreach (char c in lSystemString)
        {
            switch (c)
            {
                case 'F':
                    {
                        if (Gravitropism > 0f && state.Depth > 0)
                        {
                            Vector3 currentUp = state.Orientation * Vector3.up;
                            float horizontality = 1f - Mathf.Abs(Vector3.Dot(currentUp, Vector3.up));
                            float gravAngle = Gravitropism * horizontality * (1f + state.Depth * 0.3f);

                            Vector3 gravAxis = Vector3.Cross(currentUp, Vector3.down);
                            if (gravAxis.sqrMagnitude > 0.001f)
                            {
                                state.Orientation = Quaternion.AngleAxis(gravAngle * Mathf.Rad2Deg, gravAxis.normalized) * state.Orientation;
                            }
                        }

                        if (Phototropism > 0f && state.Depth > 0)
                        {
                            Vector3 currentDir = state.Orientation * Vector3.up;
                            Vector3 targetDir = Vector3.Slerp(currentDir, Vector3.up, Phototropism * 0.5f).normalized;
                            Quaternion bend = Quaternion.FromToRotation(currentDir, targetDir);
                            state.Orientation = bend * state.Orientation;
                        }

                        Vector3 forward = state.Orientation * Vector3.up;
                        Vector3 newPos = state.Position + forward * state.CurrentLength;

                        BranchNode newNode = new BranchNode(newPos, state.Orientation, 0f, state.Depth, state.CurrentNode);
                        newNode.Index = nodeIndex++;

                        segments.Add(new BranchSegment { Start = state.Position, End = newPos, StartRadius = 0f, EndRadius = 0f, Depth = state.Depth, StartNode = state.CurrentNode, EndNode = newNode });

                        state.Position = newPos;
                        state.CurrentNode = newNode;
                        break;
                    }
                case '+': state.Orientation *= Quaternion.Euler(0, 0, Angle + RandomVariation(rng)); break;
                case '-': state.Orientation *= Quaternion.Euler(0, 0, -(Angle + RandomVariation(rng))); break;
                case '&': state.Orientation *= Quaternion.Euler(Angle + RandomVariation(rng), 0, 0); break;
                case '^': state.Orientation *= Quaternion.Euler(-(Angle + RandomVariation(rng)), 0, 0); break;
                case '\\': state.Orientation *= Quaternion.Euler(0, Angle + RandomVariation(rng), 0); break;
                case '/': state.Orientation *= Quaternion.Euler(0, -(Angle + RandomVariation(rng)), 0); break;
                case '[':
                    stateStack.Push(state);
                    state.Depth++;
                    state.CurrentLength *= LengthDecay;
                    branchCounter++;
                    float divergence = DivergenceAngle * branchCounter + RandomVariation(rng) * 3f;
                    state.Orientation *= Quaternion.Euler(0, divergence, 0);
                    break;
                case ']':
                    if (stateStack.Count > 0) state = stateStack.Pop();
                    break;
            }
        }

        RecalculateRadii(root);

        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            seg.StartRadius = seg.StartNode.Radius;
            seg.EndRadius = seg.EndNode.Radius;
            segments[i] = seg;
        }

        return (root, segments);
    }

    private float RecalculateRadii(BranchNode node)
    {
        if (node.IsLeaf)
        {
            node.Radius = InitialRadius * 0.02f;
            return node.Radius * node.Radius;
        }
        float sumSquared = 0f;
        foreach (var child in node.Children) sumSquared += RecalculateRadii(child);

        float pipeRadius = Mathf.Sqrt(sumSquared);

        if (node.Children.Count == 1)
        {
            pipeRadius *= 1.05f;
        }

        node.Radius = Mathf.Min(pipeRadius, InitialRadius);
        return node.Radius * node.Radius;
    }

    private float RandomVariation(System.Random rng) { return (float)(rng.NextDouble() * 2 - 1) * AngleVariation; }
}