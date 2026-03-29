using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D Turtle interpreter — premeni L-system retazec na skeletalny graf stromu.
/// 
/// Podporovane symboly:
///   F  — pohyb vpred (vytvori segment vetvy)
///   +  — otocenie doprava (yaw +)
///   -  — otocenie dolava (yaw -)
///   &  — sklon dole (pitch +)
///   ^  — sklon hore (pitch -)
///   \  — naklonenie doprava (roll +)
///   /  — naklonenie dolava (roll -)
///   [  — uloz stav (zaciatok vetvy)
///   ]  — obnov stav (koniec vetvy)
///   !  — zmensi hrubku
///
/// Zmeny oproti v1:
///   - Opraveny dvojity taper (radius urcuje vylucne pipe model)
///   - Pridana gravitropia (vetvy sa ohybaju nadol)
///   - Segmenty sa aktualizuju po pipe modeli
/// </summary>
public class TurtleInterpreter
{
    public float Angle = 25f;
    public float StepLength = 1f;
    public float InitialRadius = 0.5f;
    public float RadiusDecay = 0.75f;
    public float LengthDecay = 0.85f;
    public float AngleVariation = 5f;
    public float DivergenceAngle = 137.5f;

    /// <summary>
    /// Sila gravitropie (0-1). Vetvy sa postupne ohybaju nadol.
    /// 0 = ziadna, 0.08 = mierna (realisticke), 0.3 = silna (vrba).
    /// </summary>
    public float Gravitropism = 0.08f;

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
                    // Gravitropia — mierne otocenie smerom nadol
                    if (Gravitropism > 0f && state.Depth > 0)
                    {
                        Vector3 currentUp = state.Orientation * Vector3.up;
                        float horizontality = 1f - Mathf.Abs(Vector3.Dot(currentUp, Vector3.up));
                        float gravAngle = Gravitropism * horizontality * (1f + state.Depth * 0.3f);

                        Vector3 gravAxis = Vector3.Cross(currentUp, Vector3.down);
                        if (gravAxis.sqrMagnitude > 0.001f)
                        {
                            state.Orientation = Quaternion.AngleAxis(
                                gravAngle * Mathf.Rad2Deg, gravAxis.normalized
                            ) * state.Orientation;
                        }
                    }

                    Vector3 forward = state.Orientation * Vector3.up;
                    Vector3 newPos = state.Position + forward * state.CurrentLength;

                    // Radius je docasny — pipe model ho prepocita
                    BranchNode newNode = new BranchNode(
                        newPos, state.Orientation, 0f, state.Depth, state.CurrentNode
                    );
                    newNode.Index = nodeIndex++;

                    segments.Add(new BranchSegment
                    {
                        Start = state.Position,
                        End = newPos,
                        StartRadius = 0f,
                        EndRadius = 0f,
                        Depth = state.Depth,
                        StartNode = state.CurrentNode,
                        EndNode = newNode
                    });

                    state.Position = newPos;
                    state.CurrentNode = newNode;
                    break;
                }

                case '+':
                    state.Orientation *= Quaternion.Euler(0, 0, Angle + RandomVariation(rng));
                    break;
                case '-':
                    state.Orientation *= Quaternion.Euler(0, 0, -(Angle + RandomVariation(rng)));
                    break;
                case '&':
                    state.Orientation *= Quaternion.Euler(Angle + RandomVariation(rng), 0, 0);
                    break;
                case '^':
                    state.Orientation *= Quaternion.Euler(-(Angle + RandomVariation(rng)), 0, 0);
                    break;
                case '\\':
                    state.Orientation *= Quaternion.Euler(0, Angle + RandomVariation(rng), 0);
                    break;
                case '/':
                    state.Orientation *= Quaternion.Euler(0, -(Angle + RandomVariation(rng)), 0);
                    break;

                case '[':
                    stateStack.Push(state);
                    state.Depth++;
                    state.CurrentLength *= LengthDecay;
                    branchCounter++;
                    float divergence = DivergenceAngle * branchCounter + RandomVariation(rng) * 3f;
                    state.Orientation *= Quaternion.Euler(0, divergence, 0);
                    break;

                case ']':
                    if (stateStack.Count > 0)
                        state = stateStack.Pop();
                    break;

                case '!':
                    break;
            }
        }

        // Pipe model — jediny zdroj pravdy pre polomery
        RecalculateRadii(root);

        // Aktualizuj segmenty s polomermi z pipe modelu
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            seg.StartRadius = seg.StartNode.Radius;
            seg.EndRadius = seg.EndNode.Radius;
            segments[i] = seg;
        }

        return (root, segments);
    }

    /// <summary>
    /// Pipe model (da Vinciho pravidlo): r_parent^2 = suma(r_child^2)
    /// </summary>
    private float RecalculateRadii(BranchNode node)
    {
        if (node.IsLeaf)
        {
            node.Radius = InitialRadius * 0.02f;
            return node.Radius * node.Radius;
        }

        float sumSquared = 0f;
        foreach (var child in node.Children)
            sumSquared += RecalculateRadii(child);

        node.Radius = Mathf.Sqrt(sumSquared);
        node.Radius = Mathf.Min(node.Radius, InitialRadius);
        return node.Radius * node.Radius;
    }

    private float RandomVariation(System.Random rng)
    {
        return (float)(rng.NextDouble() * 2 - 1) * AngleVariation;
    }
}
