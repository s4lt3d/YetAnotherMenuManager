using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TweenAnimator))]
public class TweenAnimatorEditor : Editor
{
    TweenAnimator comp;
    float previewT = 0f;
    bool isPlaying;

    double lastTime;

    void OnEnable()
    {
        comp = (TweenAnimator)target;
        lastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += EditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("space"));

        GUILayout.Space(6);
        GUILayout.Label("Position", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fromPosition"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("toPosition"));
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capture From"))
            CapturePosition(true);
        if (GUILayout.Button("Capture To"))
            CapturePosition(false);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Rotation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fromRotation"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("toRotation"));
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capture From"))
            CaptureRotation(true);
        if (GUILayout.Button("Capture To"))
            CaptureRotation(false);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Scale", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fromScale"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("toScale"));
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capture From"))
            CaptureScale(true);
        if (GUILayout.Button("Capture To"))
            CaptureScale(false);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Timing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("duration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("delay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ease"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("playOnStart"));

        GUILayout.Space(6);
        GUILayout.Label("Looping", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("loops"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("loopEase"));

        GUILayout.Space(6);
        GUILayout.Label("Events", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onStart"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onPlay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onComplete"));

        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(10);
        GUILayout.Label("Editor Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Play"))
        {
            previewT = 0f;
            comp.PreviewFrom();
            isPlaying = true;
            lastTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Stop"))
        {
            isPlaying = false;
        }

        if (GUILayout.Button("Rewind"))
        {
            isPlaying = false;
            previewT = 0f;
            comp.PreviewFrom();
            SceneView.RepaintAll();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        previewT = EditorGUILayout.Slider("Preview", previewT, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            comp.Preview(previewT);
            SceneView.RepaintAll();
        }
    }

    void CapturePosition(bool isFrom)
    {
        if (comp == null)
            return;

        Undo.RecordObject(comp, "Capture Position");
        var value = comp.space == TweenAnimator.SpaceMode.Local
            ? comp.transform.localPosition
            : comp.transform.position;
        if (isFrom)
            comp.fromPosition = value;
        else
            comp.toPosition = value;
        EditorUtility.SetDirty(comp);
    }

    void CaptureRotation(bool isFrom)
    {
        if (comp == null)
            return;

        Undo.RecordObject(comp, "Capture Rotation");
        var value = comp.space == TweenAnimator.SpaceMode.Local
            ? comp.transform.localEulerAngles
            : comp.transform.eulerAngles;
        if (isFrom)
            comp.fromRotation = value;
        else
            comp.toRotation = value;
        EditorUtility.SetDirty(comp);
    }

    void CaptureScale(bool isFrom)
    {
        if (comp == null)
            return;

        Undo.RecordObject(comp, "Capture Scale");
        var value = comp.transform.localScale;
        if (isFrom)
            comp.fromScale = value;
        else
            comp.toScale = value;
        EditorUtility.SetDirty(comp);
    }

    void EditorUpdate()
    {
        if (!isPlaying || comp == null)
            return;

        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - lastTime);
        lastTime = now;

        if (comp.duration <= 0f)
            return;

        previewT += delta / comp.duration;

        if (previewT >= 1f)
        {
            previewT = 1f;
            isPlaying = false;
        }

        comp.Preview(previewT);
        SceneView.RepaintAll();
    }
}
