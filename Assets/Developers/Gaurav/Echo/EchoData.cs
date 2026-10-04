using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum EchoActionFlags : byte
{
    None = 0,
    Fire = 1 << 0,
    Jump = 1 << 1,
    Sprint = 1 << 2,
    ADS = 1 << 3
}

[Serializable]
public struct EchoSnapshot
{
    public float Time;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 AimPoint;
    public EchoActionFlags Actions;

    public EchoSnapshot(float time, Vector3 pos, Quaternion rot, Vector3 aim, EchoActionFlags actions)
    {
        Time = time;
        Position = pos;
        Rotation = rot;
        AimPoint = aim;
        Actions = actions;
    }
}

[Serializable]
public class EchoData
{
    public int AttemptNumber;
    public float TotalLifespan;
    public float TotalDamageDealt;
    public BossPartType PreferredTargetPart;
    public List<EchoSnapshot> Snapshots = new List<EchoSnapshot>(1500); // 30s @ 50Hz pre-allocated

    public void AddSnapshot(float time, Vector3 pos, Quaternion rot, Vector3 aim, EchoActionFlags actions)
    {
        Snapshots.Add(new EchoSnapshot(time, pos, rot, aim, actions));
    }

    /// <summary>
    /// Fast O(1) sampling for 50Hz fixed ticks with zero GC allocation.
    /// </summary>
    public bool Sample(float time, out Vector3 position, out Quaternion rotation, out Vector3 aimPoint, out EchoActionFlags actions)
    {
        if (Snapshots == null || Snapshots.Count == 0)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            aimPoint = Vector3.zero;
            actions = EchoActionFlags.None;
            return false;
        }

        // Past lifespan
        if (time >= TotalLifespan)
        {
            var last = Snapshots[Snapshots.Count - 1];
            position = last.Position;
            rotation = last.Rotation;
            aimPoint = last.AimPoint;
            actions = EchoActionFlags.None;
            return false; // Signals end of life
        }

        // 50Hz fixed-interval index lookup
        float tickTime = 0.02f;
        int indexA = Mathf.Clamp(Mathf.FloorToInt(time / tickTime), 0, Snapshots.Count - 1);
        int indexB = Mathf.Clamp(indexA + 1, 0, Snapshots.Count - 1);

        EchoSnapshot a = Snapshots[indexA];
        EchoSnapshot b = Snapshots[indexB];

        float delta = b.Time - a.Time;
        float t = delta > 0.0001f ? Mathf.Clamp01((time - a.Time) / delta) : 0f;

        position = Vector3.Lerp(a.Position, b.Position, t);
        rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t);
        aimPoint = Vector3.Lerp(a.AimPoint, b.AimPoint, t);
        actions = a.Actions; // Discrete actions trigger on snapshot tick

        return true;
    }
}