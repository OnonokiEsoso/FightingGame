using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }
    public bool IsShaking => Time.unscaledTime < shakeUntil;
    public Vector3 RestPosition { get; private set; }
    public float CurrentStrength { get; private set; }
    float shakeUntil;
    void Awake() { Instance = this; RestPosition = transform.localPosition; }
    public void Shake(int frames, float strength)
    {
        if (frames <= 0 || strength <= 0) return;
        CurrentStrength = IsShaking ? Mathf.Max(CurrentStrength, strength) : strength;
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + FrameTiming.Seconds(frames));
    }
    void LateUpdate()
    {
        if (IsShaking)
        {
            Vector2 offset = Random.insideUnitCircle * CurrentStrength;
            transform.localPosition = RestPosition + new Vector3(offset.x, offset.y, 0);
        }
        else ResetShake();
    }
    public void ResetShake() { shakeUntil = 0; CurrentStrength = 0; transform.localPosition = RestPosition; }
    void OnDisable() { ResetShake(); if (Instance == this) Instance = null; }
}
