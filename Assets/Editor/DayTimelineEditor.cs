using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DayTimeline))]
public sealed class DayTimelineEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool changed = DrawDefaultInspector();
        if (changed)
        {
            DayTimeline timeline = (DayTimeline)target;
            if (timeline.PanelClip != null)
                timeline.PanelClip.anchoredPosition = timeline.PanelClipPosition;
            if (EditorApplication.isPlaying) DayTimelinePlaySave.RecordCurrent(timeline);
        }

        float cardWidth = serializedObject.FindProperty("cardWidth").floatValue;
        float cardHeight = serializedObject.FindProperty("cardHeight").floatValue;
        float cardGap = serializedObject.FindProperty("cardGap").floatValue;
        float lineHeight = serializedObject.FindProperty("cardLineHeight").floatValue;
        float overlap = serializedObject.FindProperty("cardOverlap").floatValue;
        float pegSize = serializedObject.FindProperty("pegSize").floatValue;
        float startX = serializedObject.FindProperty("cardStartX").floatValue;
        float topInset = serializedObject.FindProperty("cardTopInset").floatValue;
        float lineOffsetY = serializedObject.FindProperty("cardLineOffsetY").floatValue;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Prévia do varal", EditorStyles.boldLabel);
        Rect area = GUILayoutUtility.GetRect(10f, 150f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(0.16f, 0.16f, 0.15f));

        float stride = Mathf.Max(12f, cardWidth + cardGap - overlap);
        float widthNeeded = startX + cardWidth + stride + 12f;
        float heightNeeded = topInset + pegSize * 0.5f + cardHeight + 12f;
        float scale = Mathf.Min(1f, (area.width - 8f) / widthNeeded,
            (area.height - 8f) / heightNeeded);
        float left = area.x + 4f + startX * scale;
        float top = area.y + 4f + (topInset + pegSize * 0.5f) * scale;
        float width = cardWidth * scale;
        float height = cardHeight * scale;
        float lineY = top - pegSize * 0.35f * scale - lineOffsetY * scale;
        EditorGUI.DrawRect(new Rect(left, lineY - lineHeight * scale * 0.5f,
            (cardWidth + stride) * scale, Mathf.Max(1f, lineHeight * scale)),
            new Color(0.67f, 0.59f, 0.43f));

        DrawCard(new Rect(left, top, width, height), "CARTA 1");
        DrawCard(new Rect(left + stride * scale, top, width, height), "CARTA 2");
        float pin = pegSize * scale;
        EditorGUI.DrawRect(new Rect(left + (width - pin) * 0.5f, top - pin * 0.5f,
            pin, pin), new Color(0.16f, 0.15f, 0.12f));
        EditorGUI.DrawRect(new Rect(left + stride * scale + (width - pin) * 0.5f,
            top - pin * 0.5f, pin, pin), new Color(0.16f, 0.15f, 0.12f));
        EditorGUILayout.HelpBox("A prévia mostra as proporções. Durante o Play Mode, " +
            "os campos das cartas e o Deslocamento do painel são salvos ao sair. " +
            "Também é possível mover o Recorte da linha do tempo, ou o painel com a aba aberta, na Hierarquia.",
            MessageType.Info);
    }

    private static void DrawCard(Rect rect, string title)
    {
        EditorGUI.DrawRect(rect, new Color(0.84f, 0.78f, 0.63f));
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9
        };
        style.normal.textColor = new Color(0.19f, 0.16f, 0.12f);
        GUI.Label(rect, title, style);
    }
}
