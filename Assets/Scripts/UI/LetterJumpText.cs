using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class LetterJumpText : Text
{
    [SerializeField, Min(0)] private float jumpHeight = 14f;
    private float revealProgress;

    public void Reveal(string message, float duration)
    {
        DOTween.Kill(this);
        text = message;
        revealProgress = 0;
        SetVerticesDirty();
        int glyphCount = Mathf.Max(1, message.Length);
        DOTween.To(() => revealProgress, value =>
        {
            revealProgress = value;
            SetVerticesDirty();
        }, glyphCount, duration).SetEase(Ease.Linear).SetTarget(this);
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        base.OnPopulateMesh(vertexHelper);
        int count = vertexHelper.currentVertCount / 4;
        int shown = Mathf.CeilToInt(revealProgress);
        float fraction = revealProgress - Mathf.Floor(revealProgress);
        for (int i = 0; i < count; i++)
        {
            bool hidden = i >= shown;
            float offset = i == shown - 1 && fraction > 0.001f ? jumpHeight * (1f - fraction) : 0f;
            for (int corner = 0; corner < 4; corner++)
            {
                int index = i * 4 + corner;
                UIVertex vertex = new UIVertex();
                vertexHelper.PopulateUIVertex(ref vertex, index);
                if (hidden) vertex.color.a = 0;
                vertex.position.y += offset;
                vertexHelper.SetUIVertex(vertex, index);
            }
        }
    }
}
