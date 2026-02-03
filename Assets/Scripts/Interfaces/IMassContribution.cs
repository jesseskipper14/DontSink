using UnityEngine;

public interface IMassContribution
{
    float MassContribution { get; }
    Vector2 WorldCenterOfMass { get; }
}

