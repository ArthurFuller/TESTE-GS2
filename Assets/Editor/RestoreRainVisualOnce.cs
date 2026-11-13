using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class RestoreRainVisualOnce
{
    [InitializeOnLoadMethod]
    private static void Schedule() => EditorApplication.delayCall += Restore;

    private static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !scene.path.EndsWith("/PrototypeLayout.unity")) continue;
            ParticleSystem rain = null;
            ParticleSystem splash = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (system.name == "Chuva - respingos no solo") splash = system;
                    else if (system.name == "Chuva") rain = system;
                }
            if (rain == null || splash == null) return;

            var rainMain = rain.main;
            rainMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var collision = rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.dampen = 1f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;

            var splashMain = splash.main;
            splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            splashMain.loop = false;
            var emission = splash.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1, 1) });
            var size = splash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.5f)));
            var color = splash.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            EditorUtility.SetDirty(rain);
            EditorUtility.SetDirty(splash);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }
    }
}
