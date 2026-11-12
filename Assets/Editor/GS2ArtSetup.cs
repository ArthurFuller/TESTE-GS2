using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Ferramenta repetível: cria clips reais no projeto e coloca a arte na hierarquia.
public static class GS2ArtSetup
{
    private const string Art = "Assets/Arte/";
    private const string Anim = Art + "Animacoes/";
    private const string Request = "Library/GS2ArtApply.request";

    [InitializeOnLoadMethod]
    private static void ApplyWhenRequested()
    {
        if (!File.Exists(Request)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try { Apply(); File.Delete(Request); }
            catch (Exception e) { Debug.LogException(e); }
        };
    }

    [MenuItem("GS2/Arte/Aplicar sprites e animacoes 2D")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Saia do Play Mode antes de aplicar a arte.");
        Scene originalActive = SceneManager.GetActiveScene();
        Scene gameplay = SceneManager.GetSceneByPath("Assets/Scenes/PrototypeLayout.unity");
        Scene menu = SceneManager.GetSceneByPath("Assets/Scenes/MainMenu.unity");
        if ((gameplay.isLoaded && gameplay.isDirty) || (menu.isLoaded && menu.isDirty))
            throw new InvalidOperationException("Salve as cenas abertas antes de aplicar a arte.");
        bool openedGameplay = !gameplay.isLoaded;
        bool openedMenu = !menu.isLoaded;
        if (openedGameplay) gameplay = EditorSceneManager.OpenScene("Assets/Scenes/PrototypeLayout.unity", OpenSceneMode.Additive);
        if (openedMenu) menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Additive);

        Directory.CreateDirectory(Anim);
        Cropped(Art + "Barras MiniGames/Barra Coletar Madeira/Base_ColetarMadeira.png");
        Sprite[] idle = Frames(Art + "SpriteSheets/SpriteSheetAlimentacaoIdle.png");
        Sprite[] ok = Frames(Art + "SpriteSheets/SpriteSheet_AlimentacaoOk.png");
        Sprite[] good = Frames(Art + "SpriteSheets/SpriteSheet_AlimentacaoBoa.png");
        if (idle.Length != 4 || ok.Length != 3 || good.Length != 3)
            throw new InvalidOperationException("Recortes do tatu incompletos no Sprite Editor.");
        AnimationClip idleClip = SpriteClip("Tatu_Idle", idle, 12, true);
        AnimationClip okClip = SpriteClip("Tatu_Ok", ok, 6, true);
        AnimationClip goodClip = SpriteClip("Tatu_Boa", good, 6, true);
        AnimatorController tatu = Controller("Tatu", idleClip, okClip, goodClip);
        AnimationClip feedback = FeedbackClip();
        AnimatorController feedbackController = Controller("Alimentacao_Feedback", feedback);
        Sprite[] lizardPlaying = Frames(Art + "SpriteSheets/SoundLizardSpriteSheet.png");
        Sprite[] lizardEnding = Frames(Art + "SpriteSheets/EndSoundLizardSpriteSheet.png");
        if (lizardPlaying.Length != 10 || lizardEnding.Length != 5)
            throw new InvalidOperationException("Recortes do lagarto incompletos no Sprite Editor.");
        AnimatorController lizard = Controller("Lizard",
            SpriteClip("Lizard_Tocando", lizardPlaying, 12, true),
            SpriteClip("Lizard_Desligando", lizardEnding, 6, false));

        FeedingMinigame feeding = InScene<FeedingMinigame>(gameplay).FirstOrDefault();
        if (feeding == null) throw new InvalidOperationException("Minigame de alimentacao nao encontrado.");
        SerializedProperty targets = new SerializedObject(feeding).FindProperty("workerTargets");
        for (int i = 0; i < targets.arraySize; i++)
        {
            RectTransform target = targets.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;
            if (target != null) SetupTatu(target, tatu, feedbackController, idle[0]);
        }

        CardRewardChoice reward = InScene<CardRewardChoice>(gameplay).FirstOrDefault();
        if (reward != null)
        {
            SerializedProperty buttons = new SerializedObject(reward).FindProperty("rerollButtons");
            for (int i = 0; i < buttons.arraySize; i++)
            {
                Button button = buttons.GetArrayElementAtIndex(i).objectReferenceValue as Button;
                SetButtonArt(button, Art + "Botões/BTN_Rerrolar.png", true);
                if (button != null) ((RectTransform)button.transform).sizeDelta = new Vector2(68f, 68f);
            }
        }
        // O minigame é ajustado diretamente na hierarquia pelo usuário.
        foreach (AudioSettingsUI audio in InScene<AudioSettingsUI>(gameplay))
            RemoveOldLizard(audio);
        SetupTopBarLizard(gameplay, lizard, lizardPlaying[0]);
        SetupPauseArt(gameplay);
        EditorSceneManager.MarkSceneDirty(gameplay);
        EditorSceneManager.SaveScene(gameplay);

        foreach (GameObject root in menu.GetRootGameObjects())
        {
            foreach (GS2FrontEnd front in root.GetComponentsInChildren<GS2FrontEnd>(true))
            {
                SerializedObject serialized = new SerializedObject(front);
                MainButton(serialized, "playButton", "BTN_1.png", new Vector2(206f, 90f), 30f, false);
                MainButton(serialized, "optionsButton", "BTN_2.png", new Vector2(206f, 90f), -70f, false);
                MainButton(serialized, "exitButton", "BTN_SairDoJogo.png", new Vector2(90f, 90f), -170f, true);
                MainButton(serialized, "optionsBackButton", "BTN_3.png", new Vector2(160f, 70f), -168f, false);
                MainButton(serialized, "leaderboardButton", "BTN_2.png", new Vector2(160f, 70f), -168f, false);
                MainButton(serialized, "leaderboardBackButton", "BTN_3.png", new Vector2(160f, 70f), -160f, false);
            }
            foreach (AudioSettingsUI audio in root.GetComponentsInChildren<AudioSettingsUI>(true))
                RemoveOldLizard(audio);
        }
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        if (openedMenu) EditorSceneManager.CloseScene(menu, true);
        if (openedGameplay) EditorSceneManager.CloseScene(gameplay, true);
        if (originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
        AssetDatabase.SaveAssets();
        Debug.Log("GS2 Arte: tatu, feedback, lagarto, rerrolar e botoes de sair/voltar aplicados.");
    }

    private static T[] InScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static Sprite[] Frames(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).ToArray();
    }

    private static Sprite Cropped(string sourcePath)
    {
        string folder = Art + "Recortes/";
        Directory.CreateDirectory(folder);
        string path = folder + Path.GetFileNameWithoutExtension(sourcePath) + ".asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
        if (texture == null) throw new InvalidOperationException("Textura ausente: " + sourcePath);
        var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(readable, File.ReadAllBytes(sourcePath));
        Color32[] pixels = readable.GetPixels32();
        int minX = readable.width, minY = readable.height, maxX = -1, maxY = -1;
        for (int y = 0; y < readable.height; y++)
            for (int x = 0; x < readable.width; x++)
                if (pixels[y * readable.width + x].a > 8)
                {
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
        if (maxX < 0) throw new InvalidOperationException("Imagem vazia: " + sourcePath);
        const int padding = 3;
        minX = Mathf.Max(0, minX - padding); minY = Mathf.Max(0, minY - padding);
        maxX = Mathf.Min(readable.width - 1, maxX + padding);
        maxY = Mathf.Min(readable.height - 1, maxY + padding);
        Rect bounds = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        UnityEngine.Object.DestroyImmediate(readable);
        Sprite cropped = Sprite.Create(texture, bounds, new Vector2(0.5f, 0.5f), 100f);
        cropped.name = Path.GetFileNameWithoutExtension(sourcePath) + "_Recorte";
        AssetDatabase.CreateAsset(cropped, path);
        return cropped;
    }

    private static AnimationClip SpriteClip(string name, Sprite[] frames, int count, bool loop)
    {
        string path = Anim + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = 12f;
        var keys = new ObjectReferenceKeyframe[count];
        for (int i = 0; i < count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / 12f,
                value = frames[i * frames.Length / count] };
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite"), keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip FeedbackClip()
    {
        string path = Anim + "Alimentacao_IconeFlutua.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = 12f;
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Image), "m_Color.a"),
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f/12f, 1f),
                new Keyframe(3f/12f, 1f), new Keyframe(5f/12f, 0f)));
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(RectTransform), "m_AnchoredPosition.y"),
            AnimationCurve.Linear(0f, 55f, 5f/12f, 88f));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController Controller(string name, params AnimationClip[] clips)
    {
        string path = Anim + name + ".controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var stateMachine = controller.layers[0].stateMachine;
        foreach (AnimationClip clip in clips)
        {
            string stateName = name == "Tatu" ? clip.name.Substring("Tatu_".Length) :
                name == "Lizard" ? clip.name.Substring("Lizard_".Length) : "Feedback";
            var state = stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
            if (state == null) state = stateMachine.AddState(stateName);
            state.motion = clip;
            if (clip == clips[0]) stateMachine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void SetupTatu(RectTransform target, AnimatorController tatu,
        AnimatorController feedback, Sprite firstFrame)
    {
        Image image = target.GetComponent<Image>();
        if (image == null) throw new InvalidOperationException("Card de trabalhador sem Image.");
        image.sprite = firstFrame;
        image.color = Color.white;
        image.preserveAspect = true;
        image.type = Image.Type.Simple;
        Animator animator = target.GetComponent<Animator>();
        if (animator == null) animator = target.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = tatu;
        foreach (Text label in target.GetComponentsInChildren<Text>(true)) label.enabled = false;

        Transform existing = target.Find("Feedback alimento");
        GameObject child = existing != null ? existing.gameObject :
            new GameObject("Feedback alimento", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Animator), typeof(FeedingReaction));
        RectTransform rect = (RectTransform)child.transform;
        rect.SetParent(target, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 55f);
        rect.sizeDelta = new Vector2(56f, 56f);
        Image icon = child.GetComponent<Image>();
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art +
            "Elementos Alimentação/FeedbackAlimentacaoBoa.png");
        icon.color = new Color(1f, 1f, 1f, 0f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Animator iconAnimator = child.GetComponent<Animator>();
        iconAnimator.runtimeAnimatorController = feedback;
        iconAnimator.enabled = false;
        SerializedObject serialized = new SerializedObject(child.GetComponent<FeedingReaction>());
        serialized.FindProperty("image").objectReferenceValue = icon;
        serialized.FindProperty("animator").objectReferenceValue = iconAnimator;
        serialized.FindProperty("preparedFoodIcon").objectReferenceValue = icon.sprite;
        serialized.FindProperty("gatheredFoodIcon").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(Art +
                "Elementos Alimentação/FeedbackAliemntacaoOk.png");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetButtonArt(Button button, string path, bool hideText)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        Sprite sprite = Cropped(path);
        if (image == null || sprite == null) throw new InvalidOperationException("Arte do botao ausente: " + path);
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        foreach (Text text in button.GetComponentsInChildren<Text>(true))
        {
            text.enabled = !hideText;
            if (!hideText) text.color = new Color(0.95f, 0.95f, 0.85f, 1f);
        }
    }

    private static void MainButton(SerializedObject front, string field, string asset,
        Vector2 size, float y, bool hideText)
    {
        Button button = front.FindProperty(field).objectReferenceValue as Button;
        if (button == null) return;
        SetButtonArt(button, Art + "Botões/" + asset, hideText);
        RectTransform rect = (RectTransform)button.transform;
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }

    private static void RemoveOldLizard(AudioSettingsUI audio)
    {
        SerializedProperty mute = new SerializedObject(audio).FindProperty("musicMute");
        Button button = mute.objectReferenceValue as Button;
        if (button == null) return;
        Transform existing = button.transform.Find("Lagarto do som");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
    }

    private static void SetupTopBarLizard(Scene scene, AnimatorController controller, Sprite firstFrame)
    {
        RectTransform bar = InScene<RectTransform>(scene)
            .FirstOrDefault(rect => rect.name == "Barra principal do HUD");
        if (bar == null) throw new InvalidOperationException("Barra principal do HUD ausente.");
        foreach (RectTransform widget in bar.GetComponentsInChildren<RectTransform>(true))
        {
            Vector2 position = widget.anchoredPosition;
            if (widget.name.StartsWith("Botão da linha do tempo")) position.x = 710f;
            else if (widget.name.StartsWith("Abrir ou fechar miss")) position.x = 765f;
            else if (widget.name == "Pausar ou retomar") position.x = 855f;
            else continue;
            widget.anchoredPosition = position;
        }
        Transform existing = bar.Find("Botao do lagarto");
        GameObject buttonObject = existing != null ? existing.gameObject :
            new GameObject("Botao do lagarto", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(AudioLizardButton));
        RectTransform buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.SetParent(bar, false);
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = new Vector2(810f, -21f);
        buttonRect.sizeDelta = new Vector2(42f, 40f);
        Image hitImage = buttonObject.GetComponent<Image>();
        hitImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitImage.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = hitImage;
        Transform visualTransform = buttonRect.Find("Lagarto animado");
        GameObject visualObject = visualTransform != null ? visualTransform.gameObject :
            new GameObject("Lagarto animado", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Animator), typeof(AudioLizardVisual));
        RectTransform visualRect = (RectTransform)visualObject.transform;
        visualRect.SetParent(buttonRect, false);
        visualRect.anchorMin = visualRect.anchorMax = new Vector2(0.5f, 0.5f);
        visualRect.pivot = new Vector2(0.5f, 0.5f);
        visualRect.anchoredPosition = Vector2.zero;
        visualRect.sizeDelta = new Vector2(96f, 96f);
        Image image = visualObject.GetComponent<Image>();
        image.sprite = firstFrame;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        Animator animator = visualObject.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        SerializedObject visual = new SerializedObject(visualObject.GetComponent<AudioLizardVisual>());
        visual.FindProperty("animator").objectReferenceValue = animator;
        visual.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject audioButton = new SerializedObject(buttonObject.GetComponent<AudioLizardButton>());
        audioButton.FindProperty("button").objectReferenceValue = button;
        audioButton.FindProperty("visual").objectReferenceValue = visualObject.GetComponent<AudioLizardVisual>();
        audioButton.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupPauseArt(Scene scene)
    {
        foreach (PauseToggle pause in InScene<PauseToggle>(scene))
        {
            SerializedObject serialized = new SerializedObject(pause);
            Button button = serialized.FindProperty("button").objectReferenceValue as Button;
            if (button == null) continue;
            Transform existing = button.transform.Find("Voltar ao jogo - arte");
            GameObject visual = existing != null ? existing.gameObject :
                new GameObject("Voltar ao jogo - arte", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = (RectTransform)visual.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(54f, 54f);
            Image image = visual.GetComponent<Image>();
            image.sprite = Cropped(Art + "Botões/BTN_VoltarParaOJogo.png");
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            serialized.FindProperty("resumeArtwork").objectReferenceValue = image;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetupGatheringArt(GatheringMinigame game)
    {
        SerializedObject serialized = new SerializedObject(game);
        RectTransform marker = serialized.FindProperty("marker").objectReferenceValue as RectTransform;
        if (marker == null || marker.parent is not RectTransform bar)
            throw new InvalidOperationException("Barra do minigame sem marcador na hierarquia.");
        RectTransform panel = game.GetComponent<RectTransform>();
        if (panel == null) throw new InvalidOperationException("Painel do minigame ausente.");
        panel.sizeDelta = new Vector2(300f, 260f);
        Image oldPanel = panel.GetComponent<Image>();
        if (oldPanel != null)
        {
            oldPanel.enabled = true;
            oldPanel.color = new Color(0.12f, 0.13f, 0.1f, 0.88f);
            oldPanel.raycastTarget = false;
        }
        Text heading = serialized.FindProperty("title").objectReferenceValue as Text;
        if (heading != null)
        {
            heading.gameObject.SetActive(true);
            heading.color = new Color(0.95f, 0.95f, 0.85f, 1f);
            heading.fontSize = 22;
            heading.alignment = TextAnchor.MiddleCenter;
            heading.rectTransform.sizeDelta = new Vector2(280f, 55f);
            heading.rectTransform.anchoredPosition = new Vector2(10f, -8f);
            Outline outline = heading.GetComponent<Outline>();
            if (outline == null) outline = heading.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.05f, 0.04f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
        bar.anchorMin = bar.anchorMax = new Vector2(0f, 1f);
        bar.pivot = new Vector2(0.5f, 0.5f);
        bar.sizeDelta = new Vector2(179f, 29f);
        bar.anchoredPosition = new Vector2(150f, -127f);
        Image oldBar = bar.GetComponent<Image>();
        if (oldBar != null) oldBar.enabled = false;
        Button action = serialized.FindProperty("actionButton").objectReferenceValue as Button;
        if (action != null)
        {
            RectTransform actionRect = (RectTransform)action.transform;
            actionRect.anchoredPosition = new Vector2(80f, -204f);
            actionRect.sizeDelta = new Vector2(140f, 52f);
            SetButtonArt(action, Art + "Botões/BTN_1.png", false);
            Text actionLabel = action.GetComponentInChildren<Text>(true);
            if (actionLabel != null)
            {
                RectTransform labelRect = actionLabel.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(150f, 48f);
                actionLabel.alignment = TextAnchor.MiddleCenter;
                actionLabel.fontSize = 23;
            }
        }
        string woodFolder = Art + "Barras MiniGames/Barra Coletar Madeira/";
        string fishFolder = Art + "Barras MiniGames/Barra Pescaria/";
        Sprite woodBase = Cropped(woodFolder + "Base_ColetarMadeira.png");
        Sprite woodTrack = Cropped(woodFolder + "Fundo_ColetarMadeira.png");
        Sprite woodPointer = Cropped(woodFolder + "BarraVertical_ColetarMadeira.png");
        Sprite fishBase = Cropped(fishFolder + "Base_BarraPescaria.png");
        Sprite fishRed = Cropped(fishFolder + "Fronte_BarraPescaria.png");
        Sprite fishYellow = Cropped(fishFolder + "Fundo_BarraPescaria.png");
        RectTransform wood = ArtRoot(bar, "Arte madeira", new Vector2(225f, 135f));
        wood.anchoredPosition = new Vector2(2f, 24f);
        ArtLayer(wood, "Base", woodBase, Vector2.zero);
        Image woodTrackImage = ArtLayer(wood, "Fundo", woodTrack, Vector2.zero);
        PlaceLayer(woodTrackImage.rectTransform, woodTrack, woodBase, wood.rect.size);
        wood.SetAsFirstSibling();
        RectTransform fishing = ArtRoot(bar, "Arte pesca", new Vector2(119f, 230f));
        fishing.anchoredPosition = new Vector2(8f, 3f);
        ArtLayer(fishing, "Base", fishBase, Vector2.zero);
        Image fishBack = ArtLayer(fishing, "Fundo", fishYellow, Vector2.zero);
        PlaceLayer(fishBack.rectTransform, fishYellow, fishBase, fishing.rect.size);
        Image fill = ArtLayer(fishing, "Frente", fishRed, Vector2.zero);
        PlaceLayer(fill.rectTransform, fishRed, fishBase, fishing.rect.size);
        fill.transform.SetAsLastSibling();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 0.3f;
        fishing.SetSiblingIndex(1);
        fishing.gameObject.SetActive(false);
        Transform oldMarkerArt = marker.Find("Arte marcador madeira");
        if (oldMarkerArt != null) UnityEngine.Object.DestroyImmediate(oldMarkerArt.gameObject);
        marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = new Vector2(14f, 40f);
        marker.anchoredPosition = new Vector2(marker.anchoredPosition.x, 0f);
        Image pointer = marker.GetComponent<Image>();
        pointer.sprite = woodPointer;
        pointer.color = Color.white;
        pointer.preserveAspect = true;
        RectTransform target = serialized.FindProperty("greenTarget").objectReferenceValue as RectTransform;
        if (target == null) throw new InvalidOperationException("Zona alvo ausente.");
        target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
        target.pivot = new Vector2(0.5f, 0.5f);
        target.sizeDelta = new Vector2(42f, 29f);
        target.anchoredPosition = new Vector2(target.anchoredPosition.x, 0f);
        Image highlight = target.GetComponent<Image>();
        highlight.sprite = null;
        highlight.color = new Color(0.45f, 0.9f, 0.2f, 0.28f);
        highlight.preserveAspect = false;
        TargetTick(target, "Limite esquerdo", null, 0f);
        TargetTick(target, "Limite direito", null, 1f);
        serialized.FindProperty("woodArtRoot").objectReferenceValue = wood;
        serialized.FindProperty("fishingArtRoot").objectReferenceValue = fishing;
        serialized.FindProperty("fishingFill").objectReferenceValue = fill;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform ArtRoot(Transform parent, string name, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject objectToUse = existing != null ? existing.gameObject :
            new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)objectToUse.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image ArtLayer(RectTransform parent, string name, Sprite sprite, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject objectToUse = existing != null ? existing.gameObject :
            new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)objectToUse.transform;
        rect.SetParent(parent, false);
        if (size == Vector2.zero)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
        Image image = objectToUse.GetComponent<Image>();
        image.sprite = sprite;
        if (image.sprite == null) throw new InvalidOperationException("Sprite ausente: " + name);
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static void PlaceLayer(RectTransform rect, Sprite layer, Sprite reference, Vector2 referenceSize)
    {
        Rect source = reference.rect;
        Rect part = layer.rect;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(part.width / source.width * referenceSize.x,
            part.height / source.height * referenceSize.y);
        rect.anchoredPosition = new Vector2(
            (part.center.x - source.center.x) / source.width * referenceSize.x,
            (part.center.y - source.center.y) / source.height * referenceSize.y);
    }

    private static void TargetTick(RectTransform target, string name, Sprite art, float x)
    {
        Transform existing = target.Find(name);
        GameObject objectToUse = existing != null ? existing.gameObject :
            new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)objectToUse.transform;
        rect.SetParent(target, false);
        rect.anchorMin = rect.anchorMax = new Vector2(x, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(3f, 34f);
        Image image = objectToUse.GetComponent<Image>();
        image.sprite = art;
        image.color = new Color(0.65f, 1f, 0.24f, 1f);
        image.preserveAspect = false;
        image.raycastTarget = false;
    }
}
