using UnityEditor;

[CustomEditor(typeof(TopBarAutoHide))]
public sealed class TopBarAutoHideEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (!DrawDefaultInspector() || !EditorApplication.isPlaying) return;
        var bar = (TopBarAutoHide)target;
        bar.PreviewCurrentPosition();
        TopBarAutoHidePlaySave.RecordCurrent(bar);
    }
}
