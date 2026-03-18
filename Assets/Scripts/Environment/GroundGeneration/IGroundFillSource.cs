using System;

public interface IGroundFillBottomSource
{
    float LastUsedBottomY { get; }
    event Action<float> OnBottomYChanged;
}