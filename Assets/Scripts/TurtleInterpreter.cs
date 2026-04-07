using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D Turtle interpreter — premeni L-system retazec na skeletalny graf stromu
/// </summary>
public class TurtleInterpreter
{
    public float Angle = 25f; /// zakladny uhol rotacie pri symboloch + - & ^ \ /
    public float StepLength = 1f; /// zakladná dlzka jedného kroku pri symbole F
    public float InitialRadius = 0.5f; /// pociatocny polomer kmena pri koreni
    public float RadiusDecay = 0.75f; /// postupne zmensovanie hrubky
    public float LengthDecay = 0.85f; /// pri vetveni sa dlzka dalsich vetiev zmensuje
    public float AngleVariation = 5f; /// nahodna odchylka uhla (prirodzenejsi vzhlad)
    public float DivergenceAngle = 137.5f; /// vyuzitie pri divergencii vetiev (aby sa vetvy neprekryvali)

    public float Gravitropism = 0.08f; /// sila gravitropizmu - ako velmi sa vetvy ohybaju nadol
    public float Phototropism = 0.05f; /// sila fototropizmu — ako velmi sa vetvy tahaju nahor k svetlu

    public int Seed = 42; /// nejaky seed

    /// <summary>
    /// aktualny stav "korytnacky" pocas interpretacie
    /// uchovava:
    /// - poziciu
    /// - orientaciu
    /// - aktualnu dlzku kroku
    /// - hlbku vetvenia
    /// - aktualny uzol stromu
    /// </summary>
    private struct TurtleState
    {
        public Vector3 Position;
        public Quaternion Orientation;
        public float CurrentLength;
        public int Depth;
        public BranchNode CurrentNode;
    }

    /// <summary>
    /// Interpretuje L-system reťazec a vytvorí stromovú kostru:
    /// - root = koreň stromu
    /// - segments = všetky segmenty vetiev medzi uzlami
    /// </summary>
    public (BranchNode root, List<BranchSegment> segments) Interpret(string lSystemString)
    {
        System.Random rng = new System.Random(Seed); /// nahodny generator so zadanym seedom
        BranchNode root = new BranchNode(Vector3.zero, Quaternion.identity, InitialRadius, 0); /// koren stromu v strede sveta bez rotacie
        root.Index = 0;

        /// pociatocny stav turtle
        TurtleState state = new TurtleState
        {
            Position = Vector3.zero,
            Orientation = Quaternion.identity,
            CurrentLength = StepLength,
            Depth = 0,
            CurrentNode = root
        };

        Stack<TurtleState> stateStack = new Stack<TurtleState>(); /// zasobnik stavov pre vetvenie ([ a ])
        List<BranchSegment> segments = new List<BranchSegment>(); /// zoznam všetkych segmentov vetiev
        int nodeIndex = 1;  /// cislovanie uzlov
        int branchCounter = 0; /// pocitanie vetiev pre divergence angle

        /// prechadzanie znak po znaku cez l-sys retazec
        foreach (char c in lSystemString)
        {
            switch (c)
            {
                case 'F':
                    {
                        /// GRAVITROPIZMUS = vetvy sa jemne ohybaju nadol
                        /// pouziva sa len mimo kmena (Depth > 0)
                        if (Gravitropism > 0f && state.Depth > 0)
                        {
                            Vector3 currentUp = state.Orientation * Vector3.up; /// aktualny smer "hore" podla orientacie vetvy
                            float horizontality = 1f - Mathf.Abs(Vector3.Dot(currentUp, Vector3.up)); /// kontrola ako velmi je vetva horizontalna (0 - vert, 1 - horiz)
                            float gravAngle = Gravitropism * horizontality * (1f + state.Depth * 0.3f); /// uhol ohybu (gravito * horizontalita * hlbka vetvy)

                            Vector3 gravAxis = Vector3.Cross(currentUp, Vector3.down); /// osa rotacie pre ohyb smerom nadol
                            if (gravAxis.sqrMagnitude > 0.001f) /// ak je os dostatocne velka, aplikuj ohyb
                            {
                                state.Orientation = Quaternion.AngleAxis(gravAngle * Mathf.Rad2Deg, gravAxis.normalized) * state.Orientation;
                            }
                        }

                        /// FOTOTROPIZMUS = vetvy sa tahaju nahor za svetlom
                        /// tiez len mimo kmena (Depth > 0)
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
        node.Radius = Mathf.Sqrt(sumSquared);
        node.Radius = Mathf.Min(node.Radius, InitialRadius);
        return node.Radius * node.Radius;
    }

    private float RandomVariation(System.Random rng) { return (float)(rng.NextDouble() * 2 - 1) * AngleVariation; }
}