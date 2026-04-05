using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Strand — jeden "cievny zvazok" (pipe) bezici od koncovej vetvy az ku korenu.
/// Podla clanku Li et al. 2024: strand je generalizovany cylinder s fixnym polomerom,
/// ktory definuje objemovu strukturu vetvy.
/// 
/// Kazdy strand ma:
///   - Fixny polomer (rovnaky po celej dlzke)
///   - Cestu od listoveho uzla ku korenu (zoznam uzlov)
///   - 2D poziciu na kazdom priereze (backplane) — tato pozicia sa meni pozdlz cesty
/// </summary>
public class Strand
{
    /// <summary>Unikatne ID strandu.</summary>
    public int Id;

    /// <summary>Fixny polomer strandu (rovnaky po celej dlzke).</summary>
    public float Radius;

    /// <summary>
    /// Cesta strandu od listu ku korenu — zoznam uzlov ktore strand prechadza.
    /// Path[0] = listovy uzol (zaciatocny), Path[Last] = korenovy uzol.
    /// </summary>
    public List<BranchNode> Path;

    /// <summary>
    /// 2D pozicia strand ciastocky na backplane kazdeho uzla.
    /// Kluc = BranchNode, Hodnota = 2D pozicia na lokalnom priereze.
    /// Tieto pozizie sa pouzivaju na urcenie tvaru prierezu vetvy.
    /// </summary>
    public Dictionary<BranchNode, Vector2> ParticlePositions;

    /// <summary>
    /// 3D pozicie strandu po vyhodnoteni — interpolovane body v priestore.
    /// Tieto sa pouzivaju na generovanie final mesh.
    /// </summary>
    public List<Vector3> WorldPositions;

    public Strand(int id, float radius)
    {
        Id = id;
        Radius = radius;
        Path = new List<BranchNode>();
        ParticlePositions = new Dictionary<BranchNode, Vector2>();
        WorldPositions = new List<Vector3>();
    }

    /// <summary>Zostav cestu od daneho listoveho uzla ku korenu.</summary>
    public void BuildPath(BranchNode leafNode)
    {
        Path.Clear();
        BranchNode current = leafNode;
        while (current != null)
        {
            Path.Add(current);
            current = current.Parent;
        }
        // Path[0] = list, Path[Last] = koren
    }
}

/// <summary>
/// StrandParticle — 2D ciastocka reprezentujuca strand na jednom priereze.
/// Pouziva sa pri PBD packingu na rozlozenie strandov v priereze vetvy.
/// </summary>
public struct StrandParticle
{
    /// <summary>2D pozicia na backplane (lokalny suradnicovy system prierezu).</summary>
    public Vector2 Position;

    /// <summary>Polomer ciastocky (= polomer strandu).</summary>
    public float Radius;

    /// <summary>Referencia na strand ktoremu patri.</summary>
    public int StrandId;

    /// <summary>Rychlost pre PBD simulaciu.</summary>
    public Vector2 Velocity;
}
