using UnityEngine;

// Design units are 60 Hz frames, independent of rendered/physics callback counts.
public static class FrameTiming
{
    public const int FramesPerSecond = 60;
    public static float Seconds(int frames) => Mathf.Max(0, frames) / (float)FramesPerSecond;
}
