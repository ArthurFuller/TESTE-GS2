using System;
using DG.Tweening;
using UnityEngine;

public sealed class ForestTreeFall : MonoBehaviour
{
    [Serializable]
    private sealed class TreeParts
    {
        public Transform trunk;
        public Transform canopy;
        [NonSerialized] public Vector3 trunkPosition;
        [NonSerialized] public Vector3 canopyPosition;
        [NonSerialized] public Quaternion trunkRotation;
        [NonSerialized] public Quaternion canopyRotation;
        [NonSerialized] public bool fallen;
        [NonSerialized] public Tween tween;
    }

    [SerializeField] private TreeParts[] trees;
    [SerializeField, Range(30f, 100f)] private float fallAngle = 86f;
    [SerializeField, Min(0.1f)] private float fallDuration = 0.72f;
    [SerializeField, Min(0f)] private float impactBounceAngle = 7f;
    [SerializeField, Min(0f)] private float impactBounceDuration = 0.22f;
    [SerializeField, Min(0f)] private float impactHold = 0.12f;

    public Vector3 LastImpactPosition { get; private set; }

    private void Awake()
    {
        CaptureTrees();
    }

    private void CaptureTrees()
    {
        if (trees == null) return;
        foreach (TreeParts tree in trees)
        {
            if (tree?.trunk == null || tree.canopy == null) continue;
            tree.trunkPosition = tree.trunk.position;
            tree.canopyPosition = tree.canopy.position;
            tree.trunkRotation = tree.trunk.rotation;
            tree.canopyRotation = tree.canopy.rotation;
        }
    }

    public void FallNear(Vector3 workerPosition, Action onImpact)
    {
        LastImpactPosition = workerPosition + Vector3.up;
        if (trees == null || trees.Length == 0)
        {
            onImpact?.Invoke();
            return;
        }
        TreeParts chosen = null;
        float closest = float.PositiveInfinity;
        foreach (TreeParts tree in trees)
        {
            if (tree?.trunk == null || tree.canopy == null || tree.fallen) continue;
            float distance = (tree.trunkPosition - workerPosition).sqrMagnitude;
            if (distance >= closest) continue;
            closest = distance;
            chosen = tree;
        }
        if (chosen == null)
        {
            onImpact?.Invoke();
            return;
        }
        chosen.fallen = true;
        TreeParts selected = chosen;
        Vector3 basePosition = selected.trunkPosition - selected.trunk.up *
            selected.trunk.lossyScale.y;
        Vector3 fallDirection = selected.trunkPosition - workerPosition;
        fallDirection.y = 0f;
        if (fallDirection.sqrMagnitude < 0.04f) fallDirection = Vector3.right;
        fallDirection.Normalize();
        Vector3 axis = Vector3.Cross(Vector3.up, fallDirection).normalized;
        float angle = 0f;
        selected.tween?.Kill();
        Sequence fall = DOTween.Sequence().SetTarget(selected.trunk);
        fall.Append(DOTween.To(() => angle, value =>
        {
            angle = value;
            Quaternion rotation = Quaternion.AngleAxis(value, axis);
            selected.trunk.position = basePosition +
                rotation * (selected.trunkPosition - basePosition);
            selected.canopy.position = basePosition +
                rotation * (selected.canopyPosition - basePosition);
            selected.trunk.rotation = rotation * selected.trunkRotation;
            selected.canopy.rotation = rotation * selected.canopyRotation;
        }, fallAngle, fallDuration).SetEase(Ease.InQuad));
        fall.Append(DOTween.To(() => angle, value => angle = ApplyAngle(selected,
            basePosition, axis, value), fallAngle - impactBounceAngle,
            impactBounceDuration * 0.5f).SetEase(Ease.OutQuad));
        fall.Append(DOTween.To(() => angle, value => angle = ApplyAngle(selected,
            basePosition, axis, value), fallAngle,
            impactBounceDuration * 0.5f).SetEase(Ease.InOutSine));
        fall.AppendInterval(impactHold);
        fall.OnComplete(() =>
        {
            LastImpactPosition = selected.canopy.position;
            AudioManager.Play(SoundCue.TreeFall);
            onImpact?.Invoke();
        });
        selected.tween = fall;
    }

    private static float ApplyAngle(TreeParts tree, Vector3 basePosition, Vector3 axis, float angle)
    {
        Quaternion rotation = Quaternion.AngleAxis(angle, axis);
        tree.trunk.position = basePosition + rotation * (tree.trunkPosition - basePosition);
        tree.canopy.position = basePosition + rotation * (tree.canopyPosition - basePosition);
        tree.trunk.rotation = rotation * tree.trunkRotation;
        tree.canopy.rotation = rotation * tree.canopyRotation;
        return angle;
    }

    public void ResetTrees()
    {
        if (trees == null) return;
        foreach (TreeParts tree in trees)
        {
            if (tree?.trunk == null || tree.canopy == null) continue;
            tree.tween?.Kill();
            tree.trunk.position = tree.trunkPosition;
            tree.canopy.position = tree.canopyPosition;
            tree.trunk.rotation = tree.trunkRotation;
            tree.canopy.rotation = tree.canopyRotation;
            tree.fallen = false;
        }
    }

    private void OnDestroy()
    {
        if (trees == null) return;
        foreach (TreeParts tree in trees)
            tree?.tween?.Kill();
    }
}
