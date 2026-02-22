using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class TweenAnimator : MonoBehaviour
{
    public enum TweenTarget
    {
        Position,
        Rotation,
        Scale
    }

    public enum SpaceMode
    {
        World,
        Local
    }

    [Header("Space")]
    public SpaceMode space = SpaceMode.Local;

    [Header("Position")]
    public Vector3 fromPosition;
    public Vector3 toPosition;

    [Header("Rotation")]
    public Vector3 fromRotation;
    public Vector3 toRotation;

    [Header("Scale")]
    public Vector3 fromScale = Vector3.one;
    public Vector3 toScale = Vector3.one;

    [Header("Timing")]
    public float duration = 1f;
    public float delay = 0f;
    public LeanTweenType ease = LeanTweenType.easeOutQuad;
    public bool playOnStart = true;

    [Header("Looping")]
    public int loops = 0; // 0 = no loop, -1 = infinite
    public LeanTweenType loopEase = LeanTweenType.linear;

    [Header("Events")]
    public UnityEvent onStart;
    public UnityEvent onPlay;
    public UnityEvent onComplete;

    [FormerlySerializedAs("from")]
    [SerializeField, HideInInspector]
    private Vector3 legacyFrom;

    [FormerlySerializedAs("to")]
    [SerializeField, HideInInspector]
    private Vector3 legacyTo;

    [FormerlySerializedAs("target")]
    [SerializeField, HideInInspector]
    private TweenTarget legacyTarget = TweenTarget.Position;

    [SerializeField, HideInInspector]
    private bool legacyMigrated;

    private int positionTweenId = -1;
    private int rotationTweenId = -1;
    private int scaleTweenId = -1;
    private int onPlayTweenId = -1;

    private void Awake()
    {
        MigrateLegacyValues();
    }

    void Start()
    {
        if (playOnStart)
            Play();
    }

    private void Reset()
    {
        SetDefaultsFromCurrent();
        legacyMigrated = true;
    }

    public void Play()
    {
        Kill();
        ApplyFromValues();
        onStart?.Invoke();
        ScheduleOnPlay();

        positionTweenId = TweenPosition();
        rotationTweenId = TweenRotation();
        scaleTweenId = TweenScale();
    }

    public void Kill()
    {
        if (positionTweenId != -1)
            LeanTween.cancel(positionTweenId);
        if (rotationTweenId != -1)
            LeanTween.cancel(rotationTweenId);
        if (scaleTweenId != -1)
            LeanTween.cancel(scaleTweenId);
        if (onPlayTweenId != -1)
            LeanTween.cancel(onPlayTweenId);

        positionTweenId = -1;
        rotationTweenId = -1;
        scaleTweenId = -1;
        onPlayTweenId = -1;
    }

    int TweenPosition()
    {
        var descr = LeanTween.value(gameObject, 0f, 1f, duration)
            .setDelay(Mathf.Max(0f, delay))
            .setEase(ease)
            .setOnUpdate((float value) =>
            {
                Vector3 pos = Vector3.LerpUnclamped(fromPosition, toPosition, value);
                ApplyPosition(pos);
            })
            .setOnComplete(() => onComplete?.Invoke());
        ConfigureLoop(descr);
        return descr.id;
    }

    int TweenScale()
    {
        var descr = LeanTween.value(gameObject, 0f, 1f, duration)
            .setDelay(Mathf.Max(0f, delay))
            .setEase(ease)
            .setOnUpdate((float value) =>
            {
                transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, value);
            });
        ConfigureLoop(descr);
        return descr.id;
    }

    int TweenRotation()
    {
        // Manual Euler interpolation to support >360 rotations
        var descr = LeanTween.value(gameObject, 0f, 1f, duration)
            .setDelay(Mathf.Max(0f, delay))
            .setEase(ease)
            .setOnUpdate((float value) =>
            {
                ApplyRotation(Vector3.LerpUnclamped(fromRotation, toRotation, value));
            })
            ;
        ConfigureLoop(descr);
        return descr.id;
    }

    LeanTweenType GetLoopType()
    {
        if (loops == 0)
            return LeanTweenType.once;

        if (loopEase == LeanTweenType.pingPong || loopEase == LeanTweenType.clamp || loopEase == LeanTweenType.once)
            return loopEase;

        return LeanTweenType.clamp;
    }

    void ConfigureLoop(LTDescr descr)
    {
        if (loops == 0 || descr == null)
            return;

        var loopType = GetLoopType();
        if (loopType == LeanTweenType.pingPong)
        {
            if (loops == -1)
                descr.setLoopPingPong();
            else
                descr.setLoopPingPong(loops);
            return;
        }

        if (loopType == LeanTweenType.once)
        {
            descr.setLoopOnce();
            return;
        }

        descr.setLoopCount(loops);
    }

    void ApplyFromValues()
    {
        ApplyPosition(fromPosition);
        ApplyRotation(fromRotation);
        transform.localScale = fromScale;
    }

    void ApplyPosition(Vector3 value)
    {
        if (space == SpaceMode.Local)
            transform.localPosition = value;
        else
            transform.position = value;
    }

    void ApplyRotation(Vector3 euler)
    {
        Quaternion q = Quaternion.Euler(euler);
        if (space == SpaceMode.Local)
            transform.localRotation = q;
        else
            transform.rotation = q;
    }

    void ScheduleOnPlay()
    {
        if (delay <= 0f)
        {
            onPlay?.Invoke();
            return;
        }

        onPlayTweenId = LeanTween.delayedCall(gameObject, delay, () =>
        {
            onPlayTweenId = -1;
            onPlay?.Invoke();
        }).id;
    }

#if UNITY_EDITOR
    public void Preview(float t)
    {
        t = Mathf.Clamp01(t);
        float easedT = EvaluateEase(t);

        ApplyPosition(Vector3.LerpUnclamped(fromPosition, toPosition, easedT));
        ApplyRotation(Vector3.LerpUnclamped(fromRotation, toRotation, easedT));
        transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, easedT);
    }

    public void PreviewFrom()
    {
        ApplyFromValues();
    }

    float EvaluateEase(float t)
    {
        switch (ease)
        {
            case LeanTweenType.easeInQuad:
                return LeanTween.easeInQuad(0f, 1f, t);
            case LeanTweenType.easeOutQuad:
                return LeanTween.easeOutQuad(0f, 1f, t);
            case LeanTweenType.easeInOutQuad:
                return LeanTween.easeInOutQuad(0f, 1f, t);
            case LeanTweenType.easeInCubic:
                return LeanTween.easeInCubic(0f, 1f, t);
            case LeanTweenType.easeOutCubic:
                return LeanTween.easeOutCubic(0f, 1f, t);
            case LeanTweenType.easeInOutCubic:
                return LeanTween.easeInOutCubic(0f, 1f, t);
            case LeanTweenType.easeInQuart:
                return LeanTween.easeInQuart(0f, 1f, t);
            case LeanTweenType.easeOutQuart:
                return LeanTween.easeOutQuart(0f, 1f, t);
            case LeanTweenType.easeInOutQuart:
                return LeanTween.easeInOutQuart(0f, 1f, t);
            case LeanTweenType.easeInQuint:
                return LeanTween.easeInQuint(0f, 1f, t);
            case LeanTweenType.easeOutQuint:
                return LeanTween.easeOutQuint(0f, 1f, t);
            case LeanTweenType.easeInOutQuint:
                return LeanTween.easeInOutQuint(0f, 1f, t);
            case LeanTweenType.easeInSine:
                return LeanTween.easeInSine(0f, 1f, t);
            case LeanTweenType.easeOutSine:
                return LeanTween.easeOutSine(0f, 1f, t);
            case LeanTweenType.easeInOutSine:
                return LeanTween.easeInOutSine(0f, 1f, t);
            case LeanTweenType.easeInExpo:
                return LeanTween.easeInExpo(0f, 1f, t);
            case LeanTweenType.easeOutExpo:
                return LeanTween.easeOutExpo(0f, 1f, t);
            case LeanTweenType.easeInOutExpo:
                return LeanTween.easeInOutExpo(0f, 1f, t);
            case LeanTweenType.easeInCirc:
                return LeanTween.easeInCirc(0f, 1f, t);
            case LeanTweenType.easeOutCirc:
                return LeanTween.easeOutCirc(0f, 1f, t);
            case LeanTweenType.easeInOutCirc:
                return LeanTween.easeInOutCirc(0f, 1f, t);
            case LeanTweenType.easeInBounce:
                return LeanTween.easeInBounce(0f, 1f, t);
            case LeanTweenType.easeOutBounce:
                return LeanTween.easeOutBounce(0f, 1f, t);
            case LeanTweenType.easeInOutBounce:
                return LeanTween.easeInOutBounce(0f, 1f, t);
            case LeanTweenType.easeInBack:
                return LeanTween.easeInBack(0f, 1f, t);
            case LeanTweenType.easeOutBack:
                return LeanTween.easeOutBack(0f, 1f, t);
            case LeanTweenType.easeInOutBack:
                return LeanTween.easeInOutBack(0f, 1f, t);
            case LeanTweenType.easeInElastic:
                return LeanTween.easeInElastic(0f, 1f, t);
            case LeanTweenType.easeOutElastic:
                return LeanTween.easeOutElastic(0f, 1f, t);
            case LeanTweenType.easeInOutElastic:
                return LeanTween.easeInOutElastic(0f, 1f, t);
            case LeanTweenType.easeSpring:
                return LeanTween.spring(0f, 1f, t);
            case LeanTweenType.punch:
                return LeanTween.punch.Evaluate(t);
            case LeanTweenType.easeShake:
                return LeanTween.shake.Evaluate(t);
            default:
                return t;
        }
    }
#endif

    private void OnValidate()
    {
        MigrateLegacyValues();
    }

    void SetDefaultsFromCurrent()
    {
        if (space == SpaceMode.Local)
        {
            fromPosition = transform.localPosition;
            toPosition = transform.localPosition;
            fromRotation = transform.localEulerAngles;
            toRotation = transform.localEulerAngles;
        }
        else
        {
            fromPosition = transform.position;
            toPosition = transform.position;
            fromRotation = transform.eulerAngles;
            toRotation = transform.eulerAngles;
        }

        fromScale = transform.localScale;
        toScale = transform.localScale;
    }

    void MigrateLegacyValues()
    {
        if (legacyMigrated)
            return;

        bool hasLegacyData = legacyFrom != Vector3.zero || legacyTo != Vector3.zero || legacyTarget != TweenTarget.Position;
        if (!hasLegacyData)
        {
            legacyMigrated = true;
            return;
        }

        SetDefaultsFromCurrent();

        switch (legacyTarget)
        {
            case TweenTarget.Position:
                fromPosition = legacyFrom;
                toPosition = legacyTo;
                break;
            case TweenTarget.Rotation:
                fromRotation = legacyFrom;
                toRotation = legacyTo;
                break;
            case TweenTarget.Scale:
                fromScale = legacyFrom;
                toScale = legacyTo;
                break;
        }

        legacyMigrated = true;
    }
}
