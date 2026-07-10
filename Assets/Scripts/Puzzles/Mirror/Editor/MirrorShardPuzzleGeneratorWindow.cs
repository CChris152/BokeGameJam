using System.Collections.Generic;
using System.IO;
using BokeGameJam.Puzzles.Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.Puzzles.Mirror.Editor
{
    public sealed class MirrorShardPuzzleGeneratorWindow : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/Prefabs/Puzzles/Mirror";

        [SerializeField] private Sprite sourceSprite;
        [SerializeField] private string outputFolder = DefaultOutputFolder;
        [SerializeField] private string prefabName = "MirrorShardPuzzlePanel";
        [SerializeField] private bool overwriteExistingPrefab = true;
        [SerializeField, Min(3)] private int shardCount = 9;
        [SerializeField] private int seed = 152;
        [SerializeField, Range(0f, 1f)] private float centerBias = 0.35f;
        [SerializeField, Min(8f)] private float snapDistance = 36f;
        [SerializeField, Min(0f)] private float scatterDistance = 220f;
        [SerializeField] private bool showTargetHints = true;
        [SerializeField, Range(0f, 1f)] private float targetHintAlpha = 0.22f;

        [Header("Broken Edge")]
        [SerializeField] private bool addBrokenEdges = true;
        [SerializeField, Range(0f, 3f)] private float crackGapWidth = 2.05f;
        [SerializeField, Range(0f, 12f)] private float chippedEdgeWidth = 7f;
        [SerializeField, Range(0f, 1f)] private float chipChance = 0.58f;
        [SerializeField, Range(0f, 1f)] private float edgeTintStrength = 0.42f;
        [SerializeField] private bool addCrackOverlay = true;
        [SerializeField, Range(0f, 1f)] private float crackLineStrength = 0.62f;
        [SerializeField, Range(0f, 1f)] private float surfaceCrackAmount = 0.18f;

        [MenuItem("Tools/BokeGameJam/Mirror Shard Puzzle Generator")]
        public static void Open()
        {
            GetWindow<MirrorShardPuzzleGeneratorWindow>("Mirror Shards");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Mirror Shard Puzzle", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates a fixed irregular shard layout from one Sprite. The result is a UI prefab that can be assigned to MirrorPuzzleFrame.Panel Prefab.",
                MessageType.Info);

            sourceSprite = (Sprite)EditorGUILayout.ObjectField("Source Sprite", sourceSprite, typeof(Sprite), false);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            prefabName = EditorGUILayout.TextField("Prefab Name", prefabName);
            overwriteExistingPrefab = EditorGUILayout.Toggle("Overwrite Existing Prefab", overwriteExistingPrefab);
            shardCount = EditorGUILayout.IntSlider("Shard Count", shardCount, 3, 24);
            seed = EditorGUILayout.IntField("Seed", seed);
            centerBias = EditorGUILayout.Slider("Center Bias", centerBias, 0f, 1f);
            snapDistance = EditorGUILayout.FloatField("Snap Distance", snapDistance);
            scatterDistance = EditorGUILayout.FloatField("Scatter Distance", scatterDistance);
            showTargetHints = EditorGUILayout.Toggle("Show Target Hints", showTargetHints);
            using (new EditorGUI.DisabledScope(!showTargetHints))
                targetHintAlpha = EditorGUILayout.Slider("Target Hint Alpha", targetHintAlpha, 0f, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Broken Edge", EditorStyles.boldLabel);
            addBrokenEdges = EditorGUILayout.Toggle("Add Broken Edges", addBrokenEdges);
            using (new EditorGUI.DisabledScope(!addBrokenEdges))
            {
                crackGapWidth = EditorGUILayout.Slider("Hairline Gap Width", crackGapWidth, 0f, 3f);
                chippedEdgeWidth = EditorGUILayout.Slider("Chipped Edge Width", chippedEdgeWidth, 0f, 12f);
                chipChance = EditorGUILayout.Slider("Missing Chip Amount", chipChance, 0f, 1f);
                edgeTintStrength = EditorGUILayout.Slider("Glass Edge Shine", edgeTintStrength, 0f, 1f);
                addCrackOverlay = EditorGUILayout.Toggle("Draw Crack Lines", addCrackOverlay);
                using (new EditorGUI.DisabledScope(!addCrackOverlay))
                {
                    crackLineStrength = EditorGUILayout.Slider("Crack Line Strength", crackLineStrength, 0f, 1f);
                    surfaceCrackAmount = EditorGUILayout.Slider("Surface Crack Amount", surfaceCrackAmount, 0f, 1f);
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(sourceSprite == null))
            {
                if (GUILayout.Button("Generate Fixed Shards", GUILayout.Height(32f)))
                    Generate();
            }
        }

        private void Generate()
        {
            if (sourceSprite == null)
            {
                EditorUtility.DisplayDialog("Mirror Shards", "Assign a source sprite first.", "OK");
                return;
            }

            string spritePath = AssetDatabase.GetAssetPath(sourceSprite);
            if (string.IsNullOrEmpty(spritePath))
            {
                EditorUtility.DisplayDialog("Mirror Shards", "Source sprite must be an asset in this project.", "OK");
                return;
            }

            string normalizedOutput = NormalizeAssetFolder(outputFolder);
            EnsureAssetFolder(normalizedOutput);

            TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            bool changedReadable = false;
            bool previousReadable = false;
            if (importer != null)
            {
                previousReadable = importer.isReadable;
                if (!previousReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    changedReadable = true;
                }
            }

            try
            {
                Texture2D sourceTexture = ExtractSpriteTexture(sourceSprite);
                ShardBuildResult buildResult = BuildShards(sourceTexture);
                string shardFolder = $"{normalizedOutput}/{SanitizeName(prefabName)}_Shards";
                EnsureAssetFolder(shardFolder);
                SaveShardSprites(buildResult, shardFolder);
                GameObject prefabRoot = BuildPanelPrefab(buildResult.Shards, buildResult.CrackOverlaySprite);

                string prefabPath = $"{normalizedOutput}/{SanitizeName(prefabName)}.prefab";
                if (!overwriteExistingPrefab)
                    prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                DestroyImmediate(prefabRoot);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Mirror Shards", $"Generated {buildResult.Shards.Count} shards:\n{prefabPath}", "OK");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                if (changedReadable && importer != null)
                {
                    importer.isReadable = previousReadable;
                    importer.SaveAndReimport();
                }
            }
        }

        private ShardBuildResult BuildShards(Texture2D sourceTexture)
        {
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            Color32[] sourcePixels = sourceTexture.GetPixels32();
            Vector2[] seeds = GenerateSeeds(width, height);
            RectInt[] bounds = new RectInt[seeds.Length];
            bool[] hasPixel = new bool[seeds.Length];
            int[] owners = new int[width * height];

            for (int i = 0; i < bounds.Length; i++)
                bounds[i] = new RectInt(width, height, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int owner = FindNearestSeed(new Vector2(x, y), seeds);
                    int pixelIndex = y * width + x;
                    owners[pixelIndex] = owner;

                    Color32 pixel = sourcePixels[pixelIndex];
                    if (pixel.a == 0)
                        continue;

                    ExpandBounds(ref bounds[owner], x, y, hasPixel[owner]);
                    hasPixel[owner] = true;
                }
            }

            Color32[] crackOverlayPixels = addBrokenEdges && addCrackOverlay
                ? BuildCrackOverlay(sourcePixels, owners, width, height)
                : null;

            List<ShardData> shards = new();
            Vector2 imageCenter = new(width * 0.5f, height * 0.5f);
            for (int i = 0; i < seeds.Length; i++)
            {
                if (!hasPixel[i])
                    continue;

                RectInt bound = bounds[i];
                bound.width += 1;
                bound.height += 1;

                Texture2D shardTexture = new(bound.width, bound.height, TextureFormat.RGBA32, false);
                Color32[] shardPixels = new Color32[bound.width * bound.height];

                for (int sy = 0; sy < bound.height; sy++)
                {
                    for (int sx = 0; sx < bound.width; sx++)
                    {
                        int sourceX = bound.x + sx;
                        int sourceY = bound.y + sy;
                        int sourceIndex = sourceY * width + sourceX;
                        int shardIndex = sy * bound.width + sx;
                        if (owners[sourceIndex] != i)
                        {
                            shardPixels[shardIndex] = new Color32(0, 0, 0, 0);
                            continue;
                        }

                        float edgeDistance = addBrokenEdges
                            ? FindEdgeDistance(owners, width, height, sourceX, sourceY, i)
                            : float.MaxValue;
                        int neighborOwnerCount = addBrokenEdges
                            ? CountOtherOwnersNear(owners, width, height, sourceX, sourceY, i, 4)
                            : 0;

                        Color32 shardPixel = ApplyBrokenEdge(sourcePixels[sourceIndex], edgeDistance, neighborOwnerCount, sourceX, sourceY, i);
                        if (crackOverlayPixels != null && shardPixel.a > 0)
                            shardPixel = BlendOverlay(shardPixel, crackOverlayPixels[sourceIndex]);

                        shardPixels[shardIndex] = shardPixel;
                    }
                }

                shardTexture.SetPixels32(shardPixels);
                shardTexture.Apply(false, false);

                Vector2 center = new(bound.x + bound.width * 0.5f, bound.y + bound.height * 0.5f);
                Vector2 anchoredPosition = center - imageCenter;
                shards.Add(new ShardData(i, bound, anchoredPosition, shardTexture));
            }

            Texture2D crackOverlayTexture = null;
            if (crackOverlayPixels != null)
            {
                crackOverlayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                crackOverlayTexture.SetPixels32(crackOverlayPixels);
                crackOverlayTexture.Apply(false, false);
            }

            return new ShardBuildResult(shards, crackOverlayTexture);
        }

        private Vector2[] GenerateSeeds(int width, int height)
        {
            System.Random random = new(seed);
            Vector2 center = new(width * 0.5f, height * 0.5f);
            float maxRadius = Mathf.Min(width, height) * 0.5f;
            Vector2[] seeds = new Vector2[shardCount];

            seeds[0] = center + RandomInsideCircle(random, maxRadius * 0.08f);
            for (int i = 1; i < shardCount; i++)
            {
                float ringT = (float)i / Mathf.Max(1, shardCount - 1);
                float angle = ringT * Mathf.PI * 2f + RandomRange(random, -0.45f, 0.45f);
                float radiusT = Mathf.Lerp(centerBias, 1f, (float)random.NextDouble());
                float radius = maxRadius * Mathf.Clamp(radiusT, 0.12f, 1f);
                Vector2 seedPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                seedPoint += RandomInsideCircle(random, maxRadius * 0.12f);
                seedPoint.x = Mathf.Clamp(seedPoint.x, 0f, width - 1f);
                seedPoint.y = Mathf.Clamp(seedPoint.y, 0f, height - 1f);
                seeds[i] = seedPoint;
            }

            return seeds;
        }

        private GameObject BuildPanelPrefab(List<ShardData> shards, Sprite crackOverlaySprite)
        {
            GameObject root = new("MirrorShardPuzzlePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(MirrorShardPuzzlePanel));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            float sourceWidth = sourceSprite.rect.width;
            float sourceHeight = sourceSprite.rect.height;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(sourceWidth + scatterDistance * 2f, sourceHeight + scatterDistance * 2f);

            Image backdrop = root.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.62f);

            GameObject board = CreateRect("Board", rootRect);
            RectTransform boardRect = board.GetComponent<RectTransform>();
            boardRect.sizeDelta = new Vector2(sourceWidth, sourceHeight);

            GameObject targets = CreateRect("Targets", boardRect);
            if (crackOverlaySprite != null)
                CreateCrackOverlay(boardRect, crackOverlaySprite);

            GameObject piecesRoot = CreateRect("Pieces", rootRect);
            List<MirrorShardPuzzlePiece> pieces = new();
            System.Random scatterRandom = new(seed + 9173);

            for (int i = 0; i < shards.Count; i++)
            {
                ShardData shard = shards[i];
                GameObject target = CreateShardImage($"Target_{i:00}", targets.transform as RectTransform, shard, showTargetHints ? targetHintAlpha : 0f);
                GameObject piece = CreateShardImage($"Shard_{i:00}", piecesRoot.transform as RectTransform, shard, 1f);

                Vector2 scatter = RandomScatter(scatterRandom, scatterDistance);
                RectTransform pieceRect = piece.GetComponent<RectTransform>();
                pieceRect.anchoredPosition = shard.TargetPosition + scatter;

                MirrorShardPuzzlePiece pieceComponent = piece.AddComponent<MirrorShardPuzzlePiece>();
                SerializedObject serializedPiece = new(pieceComponent);
                serializedPiece.FindProperty("target").objectReferenceValue = target.GetComponent<RectTransform>();
                serializedPiece.FindProperty("snapDistance").floatValue = snapDistance;
                serializedPiece.FindProperty("lockWhenPlaced").boolValue = true;
                serializedPiece.ApplyModifiedPropertiesWithoutUndo();
                pieces.Add(pieceComponent);
            }

            MirrorShardPuzzlePanel panel = root.GetComponent<MirrorShardPuzzlePanel>();
            SerializedObject serializedPanel = new(panel);
            serializedPanel.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            serializedPanel.FindProperty("panelRoot").objectReferenceValue = rootRect;
            SerializedProperty piecesProperty = serializedPanel.FindProperty("pieces");
            piecesProperty.arraySize = pieces.Count;
            for (int i = 0; i < pieces.Count; i++)
                piecesProperty.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private void CreateCrackOverlay(RectTransform parent, Sprite crackOverlaySprite)
        {
            GameObject go = CreateRect("CrackOverlay", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(sourceSprite.rect.width, sourceSprite.rect.height);

            Image image = go.AddComponent<Image>();
            image.sprite = crackOverlaySprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = Color.white;
        }

        private GameObject CreateShardImage(string objectName, RectTransform parent, ShardData shard, float alpha)
        {
            GameObject go = CreateRect(objectName, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(shard.Bounds.width, shard.Bounds.height);
            rect.anchoredPosition = shard.TargetPosition;

            Image image = go.AddComponent<Image>();
            image.sprite = shard.Sprite;
            image.preserveAspect = false;
            image.raycastTarget = alpha > 0.9f;
            image.color = new Color(1f, 1f, 1f, alpha);
            return go;
        }

        private static GameObject CreateRect(string objectName, RectTransform parent)
        {
            GameObject go = new(objectName, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            return go;
        }

        private void SaveShardSprites(ShardBuildResult buildResult, string shardFolder)
        {
            DeleteOldShardSprites(shardFolder);

            List<ShardData> shards = buildResult.Shards;
            bool hasCrackOverlay = buildResult.CrackOverlayTexture != null;
            for (int i = 0; i < shards.Count; i++)
            {
                ShardData shard = shards[i];
                string texturePath = $"{shardFolder}/shard_{i:00}.png";
                File.WriteAllBytes(texturePath, shard.Texture.EncodeToPNG());
                Object.DestroyImmediate(shard.Texture);
            }

            string crackOverlayPath = $"{shardFolder}/crack_overlay.png";
            if (hasCrackOverlay)
            {
                File.WriteAllBytes(crackOverlayPath, buildResult.CrackOverlayTexture.EncodeToPNG());
                Object.DestroyImmediate(buildResult.CrackOverlayTexture);
            }

            AssetDatabase.ImportAsset(shardFolder, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);

            for (int i = 0; i < shards.Count; i++)
            {
                string texturePath = $"{shardFolder}/shard_{i:00}.png";
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }

                shards[i].Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            }

            if (hasCrackOverlay)
            {
                TextureImporter importer = AssetImporter.GetAtPath(crackOverlayPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }

                buildResult.CrackOverlaySprite = AssetDatabase.LoadAssetAtPath<Sprite>(crackOverlayPath);
            }
        }

        private Texture2D ExtractSpriteTexture(Sprite sprite)
        {
            Rect rect = sprite.rect;
            Texture2D source = sprite.texture;
            Texture2D texture = new((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            Color[] pixels = source.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static int FindNearestSeed(Vector2 point, Vector2[] seeds)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < seeds.Length; i++)
            {
                float distance = (point - seeds[i]).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private Color32[] BuildCrackOverlay(Color32[] sourcePixels, int[] owners, int width, int height)
        {
            Color32[] overlayPixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (sourcePixels[index].a == 0)
                        continue;

                    int owner = owners[index];
                    if (!IsInternalBoundaryPixel(sourcePixels, owners, width, height, x, y, owner))
                        continue;

                    float strengthNoise = Hash01(x / 3, y / 3, seed + owner * 7919);
                    float lineStrength = crackLineStrength * Mathf.Lerp(0.68f, 1f, strengthNoise);
                    int radius = lineStrength > 0.72f ? 1 : 0;
                    if (lineStrength > 0.92f && Hash01(x, y, seed + 31337) > 0.62f)
                        radius = 2;

                    DrawCrackSpot(overlayPixels, sourcePixels, width, height, x, y, lineStrength, radius);

                    float branchRoll = Hash01(x / 5, y / 5, seed + owner * 3571);
                    if (branchRoll < surfaceCrackAmount * 0.014f)
                        DrawBranchCrack(overlayPixels, sourcePixels, width, height, x, y, owner);
                }
            }

            return overlayPixels;
        }

        private void DrawBranchCrack(Color32[] overlayPixels, Color32[] sourcePixels, int width, int height, int startX, int startY, int owner)
        {
            float angle = Hash01(startX, startY, seed + owner * 1499) * Mathf.PI * 2f;
            float length = Mathf.Lerp(18f, 58f, Hash01(startX - 13, startY + 37, seed + owner * 811));
            Vector2 position = new(startX, startY);

            for (int step = 0; step < length; step++)
            {
                float wobble = Hash01(startX + step * 11, startY - step * 7, seed + owner * 541) - 0.5f;
                angle += wobble * 0.12f;
                position += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.35f;

                int x = Mathf.RoundToInt(position.x);
                int y = Mathf.RoundToInt(position.y);
                if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1)
                    break;

                int index = y * width + x;
                if (sourcePixels[index].a == 0)
                    break;

                float fade = 1f - step / Mathf.Max(1f, length);
                DrawCrackSpot(overlayPixels, sourcePixels, width, height, x, y, crackLineStrength * 0.62f * fade, 0);
            }
        }

        private static void DrawCrackSpot(Color32[] overlayPixels, Color32[] sourcePixels, int width, int height, int centerX, int centerY, float strength, int radius)
        {
            byte highlightAlpha = (byte)Mathf.RoundToInt(150f * Mathf.Clamp01(strength));
            byte shadowAlpha = (byte)Mathf.RoundToInt(28f * Mathf.Clamp01(strength));
            Color32 highlight = new(230, 248, 255, highlightAlpha);
            Color32 shadow = new(110, 130, 150, shadowAlpha);

            BlendCrackPixel(overlayPixels, sourcePixels, width, height, centerX + 1, centerY - 1, shadow);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance > radius + 0.1f)
                        continue;

                    Color32 color = highlight;
                    if (distance > 0f)
                        color.a = (byte)Mathf.RoundToInt(color.a * Mathf.Lerp(0.62f, 0.28f, distance / Mathf.Max(1f, radius)));

                    BlendCrackPixel(overlayPixels, sourcePixels, width, height, centerX + dx, centerY + dy, color);
                }
            }
        }

        private static void BlendCrackPixel(Color32[] overlayPixels, Color32[] sourcePixels, int width, int height, int x, int y, Color32 color)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            int index = y * width + x;
            if (sourcePixels[index].a == 0)
                return;

            overlayPixels[index] = Composite(overlayPixels[index], color);
        }

        private static Color32 Composite(Color32 destination, Color32 source)
        {
            float sourceA = source.a / 255f;
            if (sourceA <= 0f)
                return destination;

            float destinationA = destination.a / 255f;
            float outputA = sourceA + destinationA * (1f - sourceA);
            if (outputA <= 0f)
                return new Color32(0, 0, 0, 0);

            byte r = (byte)Mathf.RoundToInt((source.r * sourceA + destination.r * destinationA * (1f - sourceA)) / outputA);
            byte g = (byte)Mathf.RoundToInt((source.g * sourceA + destination.g * destinationA * (1f - sourceA)) / outputA);
            byte b = (byte)Mathf.RoundToInt((source.b * sourceA + destination.b * destinationA * (1f - sourceA)) / outputA);
            byte a = (byte)Mathf.RoundToInt(outputA * 255f);
            return new Color32(r, g, b, a);
        }

        private static Color32 BlendOverlay(Color32 pixel, Color32 overlay)
        {
            float overlayA = overlay.a / 255f;
            if (overlayA <= 0f)
                return pixel;

            pixel.r = (byte)Mathf.RoundToInt(Mathf.Lerp(pixel.r, overlay.r, overlayA));
            pixel.g = (byte)Mathf.RoundToInt(Mathf.Lerp(pixel.g, overlay.g, overlayA));
            pixel.b = (byte)Mathf.RoundToInt(Mathf.Lerp(pixel.b, overlay.b, overlayA));
            return pixel;
        }

        private static bool IsInternalBoundaryPixel(Color32[] sourcePixels, int[] owners, int width, int height, int x, int y, int owner)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int px = x + dx;
                    int py = y + dy;
                    if (px < 0 || px >= width || py < 0 || py >= height)
                        continue;

                    int index = py * width + px;
                    if (sourcePixels[index].a == 0)
                        continue;

                    if (owners[index] != owner)
                        return true;
                }
            }

            return false;
        }

        private static int CountOtherOwnersNear(int[] owners, int width, int height, int x, int y, int owner, int radius)
        {
            int first = int.MinValue;
            int second = int.MinValue;
            int count = 0;
            int radiusSquared = radius * radius;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radiusSquared)
                        continue;

                    int px = x + dx;
                    int py = y + dy;
                    if (px < 0 || px >= width || py < 0 || py >= height)
                        continue;

                    int other = owners[py * width + px];
                    if (other == owner || other == first || other == second)
                        continue;

                    if (count == 0)
                        first = other;
                    else if (count == 1)
                        second = other;

                    count++;
                    if (count >= 2)
                        return count;
                }
            }

            return count;
        }

        private float FindEdgeDistance(int[] owners, int width, int height, int x, int y, int owner)
        {
            float maxDistance = Mathf.Max(1f, crackGapWidth + chippedEdgeWidth + 1f);
            int radius = Mathf.CeilToInt(maxDistance);
            float best = float.MaxValue;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int px = x + dx;
                    int py = y + dy;
                    bool outside = px < 0 || px >= width || py < 0 || py >= height;
                    if (!outside && owners[py * width + px] == owner)
                        continue;

                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance < best)
                        best = distance;
                }
            }

            return best;
        }

        private Color32 ApplyBrokenEdge(Color32 pixel, float edgeDistance, int neighborOwnerCount, int x, int y, int owner)
        {
            if (!addBrokenEdges || pixel.a == 0)
                return pixel;

            bool nearJoint = neighborOwnerCount >= 2;
            if (edgeDistance <= crackGapWidth)
                return new Color32(0, 0, 0, 0);

            float chipZoneEnd = crackGapWidth + chippedEdgeWidth + (nearJoint ? chippedEdgeWidth * 2f : 0f);
            if (edgeDistance <= chipZoneEnd)
            {
                float edgeT = 1f - Mathf.Clamp01((edgeDistance - crackGapWidth) / Mathf.Max(0.001f, chippedEdgeWidth));
                float lossT = Mathf.Clamp01(edgeT + (nearJoint ? 0.58f : 0f));
                if (IsMissingChip(x, y, owner, lossT, nearJoint))
                    return new Color32(0, 0, 0, 0);

                float tint = edgeTintStrength * lossT;
                pixel = Darken(pixel, tint * (nearJoint ? 0.62f : 0.42f));
                if (edgeDistance <= crackGapWidth + 1.1f)
                    pixel = Lighten(pixel, tint * (nearJoint ? 0.42f : 0.28f));
            }

            return pixel;
        }

        private bool IsMissingChip(int x, int y, int owner, float edgeT, bool nearJoint)
        {
            if (chipChance <= 0f || edgeT <= 0f)
                return false;
            if (!nearJoint && edgeT < 0.82f)
                return false;

            float cellSize = nearJoint ? 46f : 52f;
            float shiftedX = x + owner * 19;
            float shiftedY = y - owner * 23;
            int coarseX = Mathf.FloorToInt(shiftedX / cellSize);
            int coarseY = Mathf.FloorToInt(shiftedY / cellSize);
            float pocket = Hash01(coarseX, coarseY, seed + owner * 104729);
            float pocketChance = chipChance * (nearJoint ? Mathf.Lerp(1.2f, 2.6f, edgeT) : Mathf.Lerp(0.08f, 0.28f, edgeT));
            pocketChance = Mathf.Clamp01(pocketChance);
            if (pocket > pocketChance)
                return false;

            float localX = shiftedX - coarseX * cellSize;
            float localY = shiftedY - coarseY * cellSize;
            float baseX = nearJoint
                ? Mathf.Lerp(cellSize * 0.34f, cellSize * 0.66f, Hash01(coarseX + 11, coarseY - 17, seed + owner * 311))
                : Mathf.Lerp(cellSize * 0.18f, cellSize * 0.82f, Hash01(coarseX + 11, coarseY - 17, seed + owner * 311));
            float baseY = nearJoint
                ? Mathf.Lerp(cellSize * 0.34f, cellSize * 0.66f, Hash01(coarseX - 29, coarseY + 7, seed + owner * 613))
                : Mathf.Lerp(cellSize * 0.18f, cellSize * 0.82f, Hash01(coarseX - 29, coarseY + 7, seed + owner * 613));
            float angle = Hash01(coarseX + 5, coarseY - 3, seed + owner * 1223) * Mathf.PI * 2f;
            Vector2 forwardAxis = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 sideAxis = new(-forwardAxis.y, forwardAxis.x);

            Vector2 fromBase = new(localX - baseX, localY - baseY);
            float forward = Vector2.Dot(fromBase, forwardAxis);
            if (forward < 0f)
                return false;

            float depth = cellSize * (nearJoint
                ? Mathf.Lerp(0.95f, 1.45f, Hash01(coarseX - 17, coarseY + 23, seed + owner * 977))
                : Mathf.Lerp(0.36f, 0.68f, Hash01(coarseX + 29, coarseY + 31, seed + owner * 433)));
            if (forward > depth)
                return false;

            float halfWidth = cellSize * (nearJoint
                ? Mathf.Lerp(0.5f, 0.86f, Hash01(coarseX + 37, coarseY - 19, seed + owner * 569))
                : Mathf.Lerp(0.16f, 0.3f, Hash01(coarseX - 41, coarseY + 13, seed + owner * 887)));
            float t = forward / Mathf.Max(0.001f, depth);
            float allowedSide = Mathf.Lerp(halfWidth, halfWidth * 0.08f, t);
            float side = Mathf.Abs(Vector2.Dot(fromBase, sideAxis));
            return side <= allowedSide;
        }

        private static Color32 Lighten(Color32 pixel, float amount)
        {
            amount = Mathf.Clamp01(amount);
            pixel.r = (byte)Mathf.RoundToInt(Mathf.Lerp(pixel.r, 255f, amount));
            pixel.g = (byte)Mathf.RoundToInt(Mathf.Lerp(pixel.g, 255f, amount));
            pixel.b = (byte)Mathf.RoundToInt(Mathf.Lerp(pixel.b, 255f, amount));
            return pixel;
        }

        private static Color32 Darken(Color32 pixel, float amount)
        {
            amount = Mathf.Clamp01(amount);
            pixel.r = (byte)Mathf.RoundToInt(pixel.r * (1f - amount));
            pixel.g = (byte)Mathf.RoundToInt(pixel.g * (1f - amount));
            pixel.b = (byte)Mathf.RoundToInt(pixel.b * (1f - amount));
            return pixel;
        }

        private static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + salt * 224682251);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777216f;
            }
        }

        private static void ExpandBounds(ref RectInt bounds, int x, int y, bool hasExistingPixel)
        {
            if (!hasExistingPixel)
            {
                bounds.x = x;
                bounds.y = y;
                bounds.width = 0;
                bounds.height = 0;
                return;
            }

            int xMin = Mathf.Min(bounds.x, x);
            int yMin = Mathf.Min(bounds.y, y);
            int xMax = Mathf.Max(bounds.x + bounds.width, x);
            int yMax = Mathf.Max(bounds.y + bounds.height, y);
            bounds.x = xMin;
            bounds.y = yMin;
            bounds.width = xMax - xMin;
            bounds.height = yMax - yMin;
        }

        private static Vector2 RandomInsideCircle(System.Random random, float radius)
        {
            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        private static Vector2 RandomScatter(System.Random random, float distance)
        {
            Vector2 scatter = RandomInsideCircle(random, distance);
            if (scatter.magnitude < distance * 0.35f)
                scatter = scatter.normalized * distance * 0.35f;
            return scatter;
        }

        private static float RandomRange(System.Random random, float min, float max)
        {
            return min + (max - min) * (float)random.NextDouble();
        }

        private static string NormalizeAssetFolder(string folder)
        {
            string normalized = string.IsNullOrWhiteSpace(folder) ? DefaultOutputFolder : folder.Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/"))
                normalized = DefaultOutputFolder;
            return normalized.TrimEnd('/');
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = NormalizeAssetFolder(folder);
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SanitizeName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "MirrorShardPuzzlePanel" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name;
        }

        private static void DeleteOldShardSprites(string shardFolder)
        {
            if (!AssetDatabase.IsValidFolder(shardFolder))
                return;

            string absoluteFolder = Path.GetFullPath(shardFolder);
            if (!Directory.Exists(absoluteFolder))
                return;

            string[] oldFiles = Directory.GetFiles(absoluteFolder, "shard_*.png", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < oldFiles.Length; i++)
            {
                string assetPath = oldFiles[i].Replace('\\', '/');
                int assetsIndex = assetPath.IndexOf("Assets/", System.StringComparison.OrdinalIgnoreCase);
                if (assetsIndex >= 0)
                    AssetDatabase.DeleteAsset(assetPath[assetsIndex..]);
            }

            string overlayPath = Path.Combine(absoluteFolder, "crack_overlay.png");
            if (File.Exists(overlayPath))
            {
                string assetPath = overlayPath.Replace('\\', '/');
                int assetsIndex = assetPath.IndexOf("Assets/", System.StringComparison.OrdinalIgnoreCase);
                if (assetsIndex >= 0)
                    AssetDatabase.DeleteAsset(assetPath[assetsIndex..]);
            }
        }

        private sealed class ShardBuildResult
        {
            public readonly List<ShardData> Shards;
            public readonly Texture2D CrackOverlayTexture;
            public Sprite CrackOverlaySprite;

            public ShardBuildResult(List<ShardData> shards, Texture2D crackOverlayTexture)
            {
                Shards = shards;
                CrackOverlayTexture = crackOverlayTexture;
            }
        }

        private sealed class ShardData
        {
            public readonly int Index;
            public readonly RectInt Bounds;
            public readonly Vector2 TargetPosition;
            public readonly Texture2D Texture;
            public Sprite Sprite;

            public ShardData(int index, RectInt bounds, Vector2 targetPosition, Texture2D texture)
            {
                Index = index;
                Bounds = bounds;
                TargetPosition = targetPosition;
                Texture = texture;
            }
        }
    }
}
