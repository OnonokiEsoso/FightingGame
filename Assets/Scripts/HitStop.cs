using UnityEngine;

public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }
    public static bool IsActive => Instance && Instance.stopping;
    float resumeAt, previousScale = 1;
    bool stopping;
    void Awake() { Instance = this; }
    public void StopForFrames(int frames)
    {
        if (frames <= 0) return;
        if (!stopping) { previousScale = Time.timeScale; stopping = true; }
        resumeAt = Mathf.Max(resumeAt, Time.unscaledTime + FrameTiming.Seconds(frames));
        Time.timeScale = 0;
    }
    void Update() { if (stopping && Time.unscaledTime >= resumeAt) Resume(); }
    void Resume() { if (stopping) Time.timeScale = previousScale; stopping = false; resumeAt = 0; }
    void OnDisable() { Resume(); if (Instance == this) Instance = null; }
}
