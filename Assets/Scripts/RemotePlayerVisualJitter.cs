using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerVisualJitter : MonoBehaviour
{
    [Header("Master")]
    [SerializeField] private bool enableJitter = true;

    [Header("Simulated Latency")]
    [SerializeField, Range(0f, 1f)] private float latencySeconds = 0.15f;

    [Header("Position Jitter")]
    [SerializeField, Range(0f, 0.5f)] private float jitterIntensity = 0.04f;
    [SerializeField] private float jitterFrequency = 9f;

    [Header("Rubber-banding")]
    [SerializeField] private float rubberBandIntervalMin = 4f;
    [SerializeField] private float rubberBandIntervalMax = 9f;
    [SerializeField] private float rubberBandSnapBackExtraSeconds = 0.35f;
    [SerializeField] private float rubberBandRecoverSeconds = 0.5f;

    private const float HistoryDuration = 2f;

    private struct PoseSample
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
    }

    private readonly List<PoseSample> history = new List<PoseSample>(128);

    private Player player;
    private Transform parentTransform;
    private float perlinSeedX;
    private float perlinSeedZ;

    private float nextRubberBandTime;
    private bool rubberBandActive;
    private float rubberBandStartTime;
    private Vector3 rubberBandOffset;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
        if (player != null) parentTransform = player.transform;

        float seed = GetInstanceID() * 0.13718f;
        perlinSeedX = seed;
        perlinSeedZ = seed + 37.219f;

        ScheduleNextRubberBand();
    }

    private void LateUpdate()
    {
        if (player == null || parentTransform == null) return;
        if (player.HasInputAuthority) return;
        if (!enableJitter)
        {
            transform.position = parentTransform.position;
            transform.rotation = parentTransform.rotation;
            return;
        }

        float now = Time.time;
        RecordSample(now, parentTransform.position, parentTransform.rotation);

        SamplePose(now - latencySeconds, out Vector3 basePos, out Quaternion baseRot);

        Vector3 jitter = ComputeJitterOffset(now);
        Vector3 rubber = ComputeRubberBandOffset(now);

        transform.position = basePos + jitter + rubber;
        transform.rotation = baseRot;
    }

    private void RecordSample(float time, Vector3 pos, Quaternion rot)
    {
        history.Add(new PoseSample { time = time, position = pos, rotation = rot });

        float cutoff = time - HistoryDuration;
        int drop = 0;
        while (drop < history.Count && history[drop].time < cutoff) drop++;
        if (drop > 0) history.RemoveRange(0, drop);
    }

    private void SamplePose(float targetTime, out Vector3 pos, out Quaternion rot)
    {
        if (history.Count == 0)
        {
            pos = parentTransform.position;
            rot = parentTransform.rotation;
            return;
        }

        if (targetTime <= history[0].time)
        {
            pos = history[0].position;
            rot = history[0].rotation;
            return;
        }

        int last = history.Count - 1;
        if (targetTime >= history[last].time)
        {
            pos = history[last].position;
            rot = history[last].rotation;
            return;
        }

        for (int i = last; i > 0; i--)
        {
            if (history[i - 1].time <= targetTime && targetTime <= history[i].time)
            {
                float span = history[i].time - history[i - 1].time;
                float t = span > 0.0001f ? (targetTime - history[i - 1].time) / span : 0f;
                pos = Vector3.Lerp(history[i - 1].position, history[i].position, t);
                rot = Quaternion.Slerp(history[i - 1].rotation, history[i].rotation, t);
                return;
            }
        }

        pos = history[last].position;
        rot = history[last].rotation;
    }

    private Vector3 ComputeJitterOffset(float now)
    {
        if (jitterIntensity <= 0f) return Vector3.zero;
        float t = now * jitterFrequency;
        float nx = Mathf.PerlinNoise(perlinSeedX, t) * 2f - 1f;
        float nz = Mathf.PerlinNoise(perlinSeedZ, t) * 2f - 1f;
        return new Vector3(nx, 0f, nz) * jitterIntensity;
    }

    private Vector3 ComputeRubberBandOffset(float now)
    {
        if (!rubberBandActive && now >= nextRubberBandTime)
        {
            TriggerRubberBand(now);
        }

        if (!rubberBandActive) return Vector3.zero;

        float elapsed = now - rubberBandStartTime;
        if (elapsed >= rubberBandRecoverSeconds)
        {
            rubberBandActive = false;
            ScheduleNextRubberBand();
            return Vector3.zero;
        }

        float k = elapsed / rubberBandRecoverSeconds;
        float ease = 1f - (k * k * (3f - 2f * k));
        return rubberBandOffset * ease;
    }

    private void TriggerRubberBand(float now)
    {
        SamplePose(now - latencySeconds, out Vector3 baseNow, out _);
        SamplePose(now - latencySeconds - rubberBandSnapBackExtraSeconds, out Vector3 baseBack, out _);
        rubberBandOffset = baseBack - baseNow;
        rubberBandActive = true;
        rubberBandStartTime = now;
    }

    private void ScheduleNextRubberBand()
    {
        nextRubberBandTime = Time.time + Random.Range(rubberBandIntervalMin, rubberBandIntervalMax);
    }
}
