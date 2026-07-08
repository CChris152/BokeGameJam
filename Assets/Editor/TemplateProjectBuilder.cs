using System;
using System.Collections.Generic;
using System.IO;
using BokeGameJam.Core;
using BokeGameJam.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TemplateProjectBuilder
{
    private const string MenuSceneId = "template_menu";
    private const string GallerySceneId = "template_gallery";
    private const string ImageId = "template_image";
    private const string BgmId = "template_bgm";
    private const string SfxId = "template_click";

    private const string MenuScenePath = "Assets/Scenes/TemplateMenu.unity";
    private const string GalleryScenePath = "Assets/Scenes/TemplateGallery.unity";
    private const string ImagePath = "Assets/Resources/Art/Pictures/TemplatePreview.png";
    private const string BgmPath = "Assets/Resources/Audio/Music/TemplateBgm.wav";
    private const string SfxPath = "Assets/Resources/Audio/SFX/TemplateClick.wav";
    private const string DatabasePath = "Assets/Resources/ScriptableObjects/ResourceDefinitionDatabase.asset";
    private const string BuildPath = "Build/TemplateTest/BokeGameJamTemplate.exe";

    [MenuItem("BokeGameJam/Template/Generate Template Project")]
    public static void GenerateTemplateProject()
    {
        EnsureFolders();
        CreateTemplateImage();
        CreateTemplateAudio();
        AssetDatabase.Refresh();
        ConfigureImportedAssets();
        CreateTemplateScene(MenuScenePath, "BokeGameJam Template", "Template Menu", GallerySceneId, new Color(0.09f, 0.12f, 0.14f));
        CreateTemplateScene(GalleryScenePath, "Image Display Test", "Template Gallery", MenuSceneId, new Color(0.12f, 0.09f, 0.13f));
        UpdateResourceDatabase();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TemplateProjectBuilder] Template project generated.");
    }

    [MenuItem("BokeGameJam/Template/Build Template Test EXE")]
    public static void BuildTemplateTestExe()
    {
        GenerateTemplateProject();

        string buildDirectory = Path.GetDirectoryName(BuildPath);
        if (!string.IsNullOrEmpty(buildDirectory))
            Directory.CreateDirectory(buildDirectory);

        BuildPlayerOptions buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { MenuScenePath, GalleryScenePath },
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        UnityEditor.Build.Reporting.BuildSummary summary = report.summary;

        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"[TemplateProjectBuilder] Build failed: {summary.result}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[TemplateProjectBuilder] Build succeeded: {summary.outputPath} ({summary.totalSize} bytes)");
    }

    private static void EnsureFolders()
    {
        string[] folders =
        {
            "Assets/Scenes",
            "Assets/Resources/Art/Pictures",
            "Assets/Resources/Audio/Music",
            "Assets/Resources/Audio/SFX",
            "Assets/Resources/ScriptableObjects",
            "Build/TemplateTest"
        };

        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
    }

    private static void CreateTemplateImage()
    {
        const int width = 512;
        const int height = 320;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color top = new Color(0.12f, 0.68f, 0.86f);
        Color bottom = new Color(0.95f, 0.46f, 0.18f);
        Color accent = new Color(1f, 0.93f, 0.35f);
        Color dark = new Color(0.07f, 0.08f, 0.1f);

        for (int y = 0; y < height; y++)
        {
            float vertical = y / (height - 1f);
            Color rowColor = Color.Lerp(bottom, top, vertical);

            for (int x = 0; x < width; x++)
            {
                float wave = Mathf.Sin((x * 0.035f) + (vertical * 7f)) * 0.06f;
                texture.SetPixel(x, y, rowColor + new Color(wave, wave, wave, 0f));
            }
        }

        DrawCircle(texture, new Vector2(392f, 236f), 46f, accent);
        DrawRect(texture, 72, 80, 360, 110, new Color(0f, 0f, 0f, 0.38f), true);
        DrawRect(texture, 84, 92, 336, 86, dark, true);
        DrawRect(texture, 100, 110, 112, 12, top, true);
        DrawRect(texture, 100, 138, 250, 10, accent, true);
        DrawRect(texture, 100, 160, 196, 8, bottom, true);
        DrawRect(texture, 0, 0, width, 10, dark, true);
        DrawRect(texture, 0, height - 10, width, 10, dark, true);
        DrawRect(texture, 0, 0, 10, height, dark, true);
        DrawRect(texture, width - 10, 0, 10, height, dark, true);

        texture.Apply();
        File.WriteAllBytes(ImagePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color, bool alphaBlend)
    {
        int xMax = Mathf.Min(texture.width, x + width);
        int yMax = Mathf.Min(texture.height, y + height);

        for (int py = Mathf.Max(0, y); py < yMax; py++)
        {
            for (int px = Mathf.Max(0, x); px < xMax; px++)
            {
                Color target = alphaBlend ? Color.Lerp(texture.GetPixel(px, py), color, color.a) : color;
                target.a = 1f;
                texture.SetPixel(px, py, target);
            }
        }
    }

    private static void DrawCircle(Texture2D texture, Vector2 center, float radius, Color color)
    {
        float radiusSquared = radius * radius;

        for (int y = Mathf.Max(0, Mathf.FloorToInt(center.y - radius)); y < Mathf.Min(texture.height, Mathf.CeilToInt(center.y + radius)); y++)
        {
            for (int x = Mathf.Max(0, Mathf.FloorToInt(center.x - radius)); x < Mathf.Min(texture.width, Mathf.CeilToInt(center.x + radius)); x++)
            {
                float distanceSquared = (new Vector2(x, y) - center).sqrMagnitude;
                if (distanceSquared <= radiusSquared)
                    texture.SetPixel(x, y, color);
            }
        }
    }

    private static void CreateTemplateAudio()
    {
        WriteSineWave(BgmPath, 44100, 4f, 220f, 0.18f, true);
        WriteSineWave(SfxPath, 44100, 0.22f, 880f, 0.35f, false);
    }

    private static void WriteSineWave(string path, int sampleRate, float duration, float frequency, float amplitude, bool addHarmony)
    {
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);

        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new BinaryWriter(stream);

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + sampleCount * 2);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(sampleCount * 2);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float fadeIn = Mathf.Clamp01(t / 0.04f);
            float fadeOut = Mathf.Clamp01((duration - t) / 0.08f);
            float envelope = Mathf.Min(fadeIn, fadeOut);
            float sample = Mathf.Sin(2f * Mathf.PI * frequency * t);

            if (addHarmony)
                sample = (sample * 0.72f) + (Mathf.Sin(2f * Mathf.PI * frequency * 1.5f * t) * 0.28f);

            short value = (short)Mathf.Clamp(sample * amplitude * envelope * short.MaxValue, short.MinValue, short.MaxValue);
            writer.Write(value);
        }
    }

    private static void ConfigureImportedAssets()
    {
        AssetDatabase.ImportAsset(ImagePath, ImportAssetOptions.ForceUpdate);
        TextureImporter textureImporter = AssetImporter.GetAtPath(ImagePath) as TextureImporter;
        if (textureImporter != null)
        {
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            textureImporter.mipmapEnabled = false;
            textureImporter.SaveAndReimport();
        }

        ConfigureAudio(BgmPath);
        ConfigureAudio(SfxPath);
    }

    private static void ConfigureAudio(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null)
            return;

        importer.forceToMono = true;
        importer.loadInBackground = false;
        importer.SaveAndReimport();
    }

    private static void CreateTemplateScene(string path, string title, string subtitle, string targetSceneId, Color backgroundColor)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = Path.GetFileNameWithoutExtension(path);

        Camera camera = CreateCamera(backgroundColor);
        CreateEventSystem();
        CreateManagers();
        CreateDemoCanvas(title, subtitle, targetSceneId);

        EditorSceneManager.SaveScene(scene, path);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        Debug.Log($"[TemplateProjectBuilder] Scene saved: {path}, camera: {camera.name}");
    }

    private static Camera CreateCamera(Color backgroundColor)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = backgroundColor;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = -10f;
        camera.farClipPlane = 100f;
        return camera;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static void CreateManagers()
    {
        GameObject sceneManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/GameSceneManager.prefab");
        GameObject audioManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/GameAudioManager.prefab");

        if (sceneManagerPrefab != null)
            PrefabUtility.InstantiatePrefab(sceneManagerPrefab);
        else
            new GameObject("GameSceneManager").AddComponent<GameSceneManager>();

        if (audioManagerPrefab != null)
            PrefabUtility.InstantiatePrefab(audioManagerPrefab);
        else
            new GameObject("GameAudioManager").AddComponent<GameAudioManager>();
    }

    private static void CreateDemoCanvas(string title, string subtitle, string targetSceneId)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("TemplateDemoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = CreatePanel(canvasObject.transform, "Content", new Color(0.08f, 0.09f, 0.11f, 0.86f));
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1180f, 760f), Vector2.zero);

        Text titleText = CreateText(panel.transform, "Title", title, font, 54, TextAnchor.MiddleCenter, Color.white);
        SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1040f, 96f), new Vector2(0f, -80f));

        Text subtitleText = CreateText(panel.transform, "Subtitle", subtitle, font, 28, TextAnchor.MiddleCenter, new Color(0.86f, 0.91f, 0.95f));
        SetRect(subtitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1040f, 58f), new Vector2(0f, -148f));

        GameObject imageFrame = CreatePanel(panel.transform, "ImageFrame", new Color(0.02f, 0.03f, 0.04f, 0.82f));
        SetRect(imageFrame.GetComponent<RectTransform>(), new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(720f, 450f), Vector2.zero);

        Image previewImage = CreateImage(imageFrame.transform, "PreviewImage", new Color(1f, 1f, 1f, 1f));
        SetRect(previewImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(660f, 390f), Vector2.zero);

        Text statusText = CreateText(panel.transform, "Status", "Starting template test...", font, 24, TextAnchor.MiddleCenter, new Color(0.76f, 0.88f, 0.9f));
        SetRect(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1040f, 52f), new Vector2(0f, 150f));

        Button playSfxButton = CreateButton(panel.transform, "PlaySfxButton", "Play SFX", font);
        SetRect(playSfxButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(220f, 66f), new Vector2(-250f, 74f));

        Button toggleBgmButton = CreateButton(panel.transform, "ToggleBgmButton", "Toggle BGM", font);
        SetRect(toggleBgmButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(220f, 66f), new Vector2(0f, 74f));

        Button switchSceneButton = CreateButton(panel.transform, "SwitchSceneButton", "Switch Scene", font);
        SetRect(switchSceneButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(220f, 66f), new Vector2(250f, 74f));

        Text hintText = CreateText(panel.transform, "Hint", "Keyboard: 1 Play SFX, 2 Toggle BGM, Space Switch Scene", font, 20, TextAnchor.MiddleCenter, new Color(0.68f, 0.7f, 0.74f));
        SetRect(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1040f, 42f), new Vector2(0f, 26f));

        TemplateDemoController controller = canvasObject.AddComponent<TemplateDemoController>();
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("imageId").stringValue = ImageId;
        serializedController.FindProperty("bgmId").stringValue = BgmId;
        serializedController.FindProperty("sfxId").stringValue = SfxId;
        serializedController.FindProperty("targetSceneId").stringValue = targetSceneId;
        serializedController.FindProperty("previewImage").objectReferenceValue = previewImage;
        serializedController.FindProperty("titleText").objectReferenceValue = titleText;
        serializedController.FindProperty("statusText").objectReferenceValue = statusText;
        serializedController.FindProperty("playSfxButton").objectReferenceValue = playSfxButton;
        serializedController.FindProperty("switchSceneButton").objectReferenceValue = switchSceneButton;
        serializedController.FindProperty("toggleBgmButton").objectReferenceValue = toggleBgmButton;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        return panel;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.preserveAspect = true;
        return image;
    }

    private static Text CreateText(Transform parent, string name, string text, Font font, int fontSize, TextAnchor anchor, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.color = color;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(12, fontSize - 10);
        label.resizeTextMaxSize = fontSize;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string label, Font font)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.48f, 0.62f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.48f, 0.62f);
        colors.highlightedColor = new Color(0.23f, 0.62f, 0.78f);
        colors.pressedColor = new Color(0.12f, 0.34f, 0.48f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, "Text", label, font, 24, TextAnchor.MiddleCenter, Color.white);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        return button;
    }

    private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    private static void UpdateResourceDatabase()
    {
        ResourceDefinitionDatabase database = AssetDatabase.LoadAssetAtPath<ResourceDefinitionDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ResourceDefinitionDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ImagePath);
        AudioClip bgm = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath);
        AudioClip sfx = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxPath);
        SceneAsset menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
        SceneAsset galleryScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GalleryScenePath);

        SerializedObject serializedDatabase = new SerializedObject(database);
        UpsertSprite(serializedDatabase.FindProperty("sprites"), ImageId, sprite);
        UpsertSound(serializedDatabase.FindProperty("sounds"), BgmId, bgm, ResourceDefinitionDatabase.SoundCategory.Music, true, 0.55f);
        UpsertSound(serializedDatabase.FindProperty("sounds"), SfxId, sfx, ResourceDefinitionDatabase.SoundCategory.SFX, false, 1f);
        UpsertScene(serializedDatabase.FindProperty("scenes"), MenuSceneId, menuScene, "TemplateMenu");
        UpsertScene(serializedDatabase.FindProperty("scenes"), GallerySceneId, galleryScene, "TemplateGallery");
        serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);
    }

    private static void UpsertSprite(SerializedProperty sprites, string id, Sprite sprite)
    {
        SerializedProperty entry = FindOrAddEntry(sprites, id);
        entry.FindPropertyRelative("id").stringValue = id;
        entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
    }

    private static void UpsertSound(SerializedProperty sounds, string id, AudioClip clip, ResourceDefinitionDatabase.SoundCategory category, bool loop, float volumeScale)
    {
        SerializedProperty entry = FindOrAddEntry(sounds, id);
        entry.FindPropertyRelative("id").stringValue = id;
        entry.FindPropertyRelative("category").enumValueIndex = (int)category;
        entry.FindPropertyRelative("clip").objectReferenceValue = clip;
        entry.FindPropertyRelative("loop").boolValue = loop;
        entry.FindPropertyRelative("volumeScale").floatValue = volumeScale;
    }

    private static void UpsertScene(SerializedProperty scenes, string id, SceneAsset sceneAsset, string sceneName)
    {
        SerializedProperty entry = FindOrAddEntry(scenes, id);
        entry.FindPropertyRelative("id").stringValue = id;
        SerializedProperty sceneAssetProperty = entry.FindPropertyRelative("sceneAsset");
        if (sceneAssetProperty != null)
            sceneAssetProperty.objectReferenceValue = sceneAsset;
        entry.FindPropertyRelative("sceneName").stringValue = sceneName;
    }

    private static SerializedProperty FindOrAddEntry(SerializedProperty list, string id)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            SerializedProperty idProperty = entry.FindPropertyRelative("id");
            if (idProperty != null && string.Equals(idProperty.stringValue, id, StringComparison.Ordinal))
                return entry;
        }

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        SerializedProperty newEntry = list.GetArrayElementAtIndex(index);
        SerializedProperty newIdProperty = newEntry.FindPropertyRelative("id");
        if (newIdProperty != null)
            newIdProperty.stringValue = id;
        return newEntry;
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GalleryScenePath, true)
        };

        foreach (EditorBuildSettingsScene existingScene in EditorBuildSettings.scenes)
        {
            if (existingScene.path == MenuScenePath || existingScene.path == GalleryScenePath)
                continue;

            scenes.Add(existingScene);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
