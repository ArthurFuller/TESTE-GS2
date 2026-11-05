using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DayTimeline))]
public sealed class DayTimelineEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool changed = DrawDefaultInspector();
        if (changed && EditorApplication.isPlaying)
            DayTimelinePlaySave.RecordCurrent((DayTimeline)target);

        float cardWidth = serializedObject.FindProperty("cardWidth").floatValue;
        float cardHeight = serializedObject.FindProperty("cardHeight").floatValue;
        float cardGap = serializedObject.FindProperty("cardGap").floatValue;
        float lineHeight = serializedObject.FindProperty("cardLineHeight").floatValue;
        float startX = serializedObject.FindProperty("cardStartX").floatValue;
        float topInset = serializedObject.FindProperty("cardTopInset").floatValue;
        float lineOffsetY = serializedObject.FindProperty("cardLineOffsetY").floatValue;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Prévia das cartas e da ligação", EditorStyles.boldLabel);
        Rect area = GUILayoutUtility.GetRect(10f, 150f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(0.16f, 0.16f, 0.15f));

        float widthNeeded = startX + cardWidth * 2f + cardGap + 12f;
        float heightNeeded = topInset + cardHeight + Mathf.Abs(lineOffsetY) + 12f;
        float scale = Mathf.Min(1f, (area.width - 8f) / widthNeeded,
            (area.height - 8f) / heightNeeded);
        float left = area.x + 4f + startX * scale;
        float top = area.y + 4f + topInset * scale;
        float width = cardWidth * scale;
        float height = cardHeight * scale;
        float gap = cardGap * scale;
        float lineY = top + height * 0.5f - lineOffsetY * scale;
        EditorGUI.DrawRect(new Rect(left + width * 0.5f, lineY - lineHeight * scale * 0.5f,
            (cardWidth + cardGap) * scale, Mathf.Max(1f, lineHeight * scale)),
            new Color(0.67f, 0.59f, 0.43f));

        DrawCard(new Rect(left, top, width, height), "CARTA 1");
        DrawCard(new Rect(left + width + gap, top, width, height), "CARTA 2");
        EditorGUILayout.HelpBox("A prévia mostra as proporções. Durante o Play Mode, " +
            "alterações nos campos das cartas atualizam a aba aberta e são salvas na cena " +
            "ao sair do Play Mode.",
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
