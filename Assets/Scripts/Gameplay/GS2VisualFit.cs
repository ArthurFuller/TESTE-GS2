using UnityEngine;

// Ajusta um FBX visual sem alterar o MeshFilter, o Collider ou o Transform de gameplay.
public static class GS2VisualFit
{
    public enum Axis { Auto, LongX, LongZ }

    private static readonly Quaternion[] Orientations =
    {
        Quaternion.identity,
        Quaternion.Euler(-90f, 0f, 0f),
        Quaternion.Euler(90f, 0f, 0f),
        Quaternion.Euler(0f, 90f, 0f),
        Quaternion.Euler(0f, -90f, 0f),
        Quaternion.Euler(-90f, 90f, 0f),
        Quaternion.Euler(90f, 90f, 0f),
        Quaternion.Euler(0f, 180f, 0f)
    };

    public static bool Fit(Transform frame, Transform model, Bounds target,
        Axis axis = Axis.Auto, bool preserveProportions = true)
    {
        if (frame == null || model == null) return false;
        frame.localPosition = Vector3.zero;
        frame.localRotation = Quaternion.identity;
        frame.localScale = Vector3.one;
        model.localPosition = Vector3.zero;
        model.localScale = Vector3.one;

        Bounds best = default;
        float bestScore = float.PositiveInfinity;
        Quaternion bestRotation = Quaternion.identity;
        foreach (Quaternion rotation in Orientations)
        {
            model.localRotation = rotation;
            if (!TryMeshBounds(model, frame, out Bounds candidate)) continue;
            if (candidate.size.x < 0.0001f || candidate.size.y < 0.0001f ||
                candidate.size.z < 0.0001f) continue;

            Vector3 ratios = new Vector3(target.size.x / candidate.size.x,
                target.size.y / candidate.size.y, target.size.z / candidate.size.z);
            float highest = Mathf.Max(ratios.x, Mathf.Max(ratios.y, ratios.z));
            float lowest = Mathf.Min(ratios.x, Mathf.Min(ratios.y, ratios.z));
            float score = Mathf.Log(highest / lowest);
            if (axis != Axis.Auto)
            {
                float length = axis == Axis.LongX ? candidate.size.x : candidate.size.z;
                float depth = axis == Axis.LongX ? candidate.size.z : candidate.size.x;
                if (length < candidate.size.y) score += 5f;
                if (length < depth) score += 5f;
                if (candidate.size.y < depth) score += 3f;
            }
            if (score >= bestScore) continue;
            bestScore = score;
            best = candidate;
            bestRotation = rotation;
        }

        if (float.IsPositiveInfinity(bestScore)) return false;
        model.localRotation = bestRotation;
        Vector3 scale = new Vector3(target.size.x / best.size.x,
            target.size.y / best.size.y, target.size.z / best.size.z);
        if (preserveProportions)
        {
            float uniform = Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z));
            scale = Vector3.one * uniform;
        }
        frame.localScale = scale;
        frame.localPosition = target.center - Vector3.Scale(best.center, scale);
        return true;
    }

    private static bool TryMeshBounds(Transform model, Transform frame, out Bounds result)
    {
        result = default;
        bool found = false;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                            new Vector3(x, y, z));
                        Vector3 local = frame.InverseTransformPoint(
                            filter.transform.TransformPoint(corner));
                        if (!found) { result = new Bounds(local, Vector3.zero); found = true; }
                        else result.Encapsulate(local);
                    }
        }
        return found;
    }
}
