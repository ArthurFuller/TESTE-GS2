using UnityEngine;

[ExecuteAlways]
public sealed class EditorPanelPreview : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.hierarchyChanged -= ApplyPreview;
            UnityEditor.EditorApplication.hierarchyChanged += ApplyPreview;
            UnityEditor.EditorApplication.delayCall += ApplyPreview;
        }
    }

    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyPreview;
        UnityEditor.EditorApplication.delayCall += ApplyPreview;
    }

    private void OnDisable()
    {
        UnityEditor.EditorApplication.hierarchyChanged -= ApplyPreview;
        UnityEditor.EditorApplication.delayCall -= ApplyPreview;
    }

    private void ApplyPreview()
    {
        if (this == null || Application.isPlaying) return;
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) return;
        foreach (CanvasGroup panel in group.GetComponentsInChildren<CanvasGroup>(true))
            if (panel.gameObject.activeSelf) panel.alpha = 1f;
    }
#endif
}
