using UnityEngine;

[ExecuteAlways]
public sealed class EditorPanelPreview : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private bool previewInEditor;

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += ApplyPreview;
    }

    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyPreview;
        UnityEditor.EditorApplication.delayCall += ApplyPreview;
    }

    private void OnDisable()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyPreview;
    }

    private void ApplyPreview()
    {
        if (this == null || Application.isPlaying) return;
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = previewInEditor ? 1f : 0f;
    }
#endif
}
