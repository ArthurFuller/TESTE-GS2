using UnityEditor;
using UnityEngine;

internal static class CreateCactusPrefab
{
    [MenuItem("Tools/GS2/Criar prefab Cactus")]
    private static void Create()
    {
        const string modelPath = "Assets/Modelos/Cactus/Modelo/Cactus.fbx";
        const string materialPath = "Assets/Modelos/Cactus/Materiais/M_Cactus.mat";
        const string prefabPath = "Assets/Modelos/Cactus/Cactus.prefab";
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (model == null || material == null)
        {
            Debug.LogError("Modelo ou material do cacto não encontrado.");
            return;
        }

        GameObject instance = Object.Instantiate(model);
        instance.name = "Cactus";
        try
        {
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError("O modelo do cacto não contém renderer.");
                return;
            }
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool saved);
            if (!saved) Debug.LogError("Não foi possível salvar o prefab do cacto.");
            else Debug.Log("Prefab do cacto criado: " + prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}
