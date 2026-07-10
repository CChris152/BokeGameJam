using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BokeGameJam.Core
{
    /// <summary>
    /// 全局音频管理器。音频资源通过 ResourcesManager 解析。
    /// </summary>
    public class GameAudioManager : MonoBehaviour
    {
        private const string SfxResourceRoot = "Audio/SFX";
        private const int InitialOneShotSourceCount = 6;
        private const float AudibleSampleThreshold = 0.002f;
        private const float AudibleStartPreRollSeconds = 0.003f;

        public static GameAudioManager Instance { get; private set; }

        [Header("音量")]
        [Tooltip("BGM 主音量（0-1）")]
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.6f;
        [Tooltip("SFX 主音量（0-1）")]
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        [Header("BGM 切换")]
        [Tooltip("切换 BGM 时旧曲淡出时长（秒）")]
        [SerializeField] private float defaultBgmFadeOutDuration = 1.5f;

        [Tooltip("切换 BGM 时新曲淡入时长（秒）")]
        [SerializeField] private float defaultBgmFadeInDuration = 1.5f;

        private AudioSource bgmSourceA;
        private AudioSource bgmSourceB;
        private AudioSource bgmActiveSource;

        private readonly List<AudioSource> oneShotSources = new();
        private readonly Dictionary<string, AudioSource> loopingSfxSources = new();
        private readonly Dictionary<string, float> loopingSfxVolumeScales = new();
        private readonly Dictionary<string, AudioClip> resourcePathClips = new();
        private readonly Dictionary<AudioClip, int> audibleStartSamples = new();
        private readonly HashSet<string> missingResourcePaths = new();
        private readonly HashSet<AudioClip> unreadableAudioClips = new();

        private string currentBgmId;
        private float currentBgmVolumeScale = 1f;
        private Coroutine bgmFadeCoroutine;

        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;
        public string CurrentBgmId => currentBgmId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            bgmSourceA = CreateChildSource("BGM_A", loop: true);
            bgmSourceB = CreateChildSource("BGM_B", loop: true);
            for (int i = 0; i < InitialOneShotSourceCount; i++)
            {
                AudioSource source = CreateChildSource($"SFX_OneShot_{i + 1}", loop: false);
                oneShotSources.Add(source);
            }

            bgmActiveSource = bgmSourceA;
            PreloadResourceSfx();
            ApplyVolumes();
        }

        private void OnEnable()
        {
            if (Instance != this)
                return;

            EventManager.On<string>(GameEvents.LevelCompleted, OnLevelCompleted);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            if (Instance != this)
                return;

            EventManager.Off<string>(GameEvents.LevelCompleted, OnLevelCompleted);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            // 启动时从 DataManager 分别恢复 BGM / SFX 音量
            LoadVolumesFromDataManager();
            ApplySceneAmbience(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// 从 DataManager 分别读取 BGM / SFX 音量并应用。
        /// 若新键尚未写入，则回退到旧版 MasterVolume，再回退到当前运行时默认值。
        /// </summary>
        public void LoadVolumesFromDataManager()
        {
            if (DataManager.Instance == null)
                return;

            SetBGMVolume(ResolveSavedVolume(DataManager.Keys.BgmVolume, bgmVolume));
            SetSFXVolume(ResolveSavedVolume(DataManager.Keys.SfxVolume, sfxVolume));
        }

        /// <summary>
        /// 解析已保存音量：优先独立键，其次旧版 MasterVolume，最后使用 fallback。
        /// </summary>
        private static float ResolveSavedVolume(string volumeKey, float fallback)
        {
            DataManager data = DataManager.Instance;
            if (data == null)
                return Mathf.Clamp01(fallback);

            if (data.HasKey(volumeKey))
                return Mathf.Clamp01(data.GetFloat(volumeKey));

            if (data.HasKey(DataManager.Keys.MasterVolume))
                return Mathf.Clamp01(data.GetFloat(DataManager.Keys.MasterVolume));

            return Mathf.Clamp01(data.GetFloat(volumeKey, fallback));
        }

        #region 背景音乐

        /// <summary>
        /// 播放 BGM。已有曲目时默认先淡出再淡入；
        /// <paramref name="fadeDuration"/> &lt; 0 用默认 1.5s/1.5s，=0 立即切换，&gt;0 时淡出与淡入都用该时长。
        /// </summary>
        public void PlayBGM(ResourceDefinitionDatabase.SoundResource music, float fadeDuration = -1f)
        {
            if (!TryLoadSound(music, ResourceDefinitionDatabase.SoundCategory.Music, out AudioClip clip))
                return;

            string musicId = music.Id;
            if (currentBgmId == musicId && bgmActiveSource.isPlaying)
                return;

            ResolveFadeDurations(fadeDuration, out float fadeOut, out float fadeIn);

            if (string.IsNullOrEmpty(currentBgmId) || (fadeOut <= 0f && fadeIn <= 0f))
            {
                StopBgmFade();
                PlayBgmImmediate(clip, music, musicId);
                return;
            }

            SwitchBGM(music, fadeOut, fadeIn);
        }

        public void PlayBGMById(string musicId, float fadeDuration = -1f)
        {
            if (!ResourcesManager.TryGetSound(musicId, out ResourceDefinitionDatabase.SoundResource music))
            {
                Debug.LogError($"[GameAudioManager] Cannot find music id: {musicId}");
                return;
            }

            PlayBGM(music, fadeDuration);
        }

        /// <summary>切换 BGM：先淡出旧曲，再淡入新曲，并广播 <see cref="GameEvents.BgmSwitchProcess"/>。</summary>
        public void SwitchBGM(ResourceDefinitionDatabase.SoundResource music, float fadeDuration = -1f)
        {
            ResolveFadeDurations(fadeDuration, out float fadeOut, out float fadeIn);
            SwitchBGM(music, fadeOut, fadeIn);
        }

        /// <summary>切换 BGM（可分别指定淡出 / 淡入时长）。</summary>
        public void SwitchBGM(ResourceDefinitionDatabase.SoundResource music, float fadeOutDuration, float fadeInDuration)
        {
            if (!TryLoadSound(music, ResourceDefinitionDatabase.SoundCategory.Music, out AudioClip clip))
                return;

            string musicId = music.Id;
            if (currentBgmId == musicId && bgmActiveSource.isPlaying)
                return;

            StopBgmFade();
            bgmFadeCoroutine = StartCoroutine(SwitchBgmRoutine(clip, music, musicId, fadeOutDuration, fadeInDuration));
        }

        public void SwitchBGMById(string musicId, float fadeDuration = -1f)
        {
            if (!ResourcesManager.TryGetSound(musicId, out ResourceDefinitionDatabase.SoundResource music))
            {
                Debug.LogError($"[GameAudioManager] Cannot find music id: {musicId}");
                return;
            }

            SwitchBGM(music, fadeDuration);
        }

        public void SwitchBGMById(string musicId, float fadeOutDuration, float fadeInDuration)
        {
            if (!ResourcesManager.TryGetSound(musicId, out ResourceDefinitionDatabase.SoundResource music))
            {
                Debug.LogError($"[GameAudioManager] Cannot find music id: {musicId}");
                return;
            }

            SwitchBGM(music, fadeOutDuration, fadeInDuration);
        }

        public void StopBGM(float fadeDuration = 0f)
        {
            if (fadeDuration <= 0f)
            {
                StopBgmFade();
                bgmSourceA.Stop();
                bgmSourceB.Stop();
                currentBgmId = null;
                currentBgmVolumeScale = 1f;
                ApplyVolumes();
                return;
            }

            StopBgmFade();
            bgmFadeCoroutine = StartCoroutine(FadeOutAndStopBgmRoutine(fadeDuration));
        }

        public void PauseBGM() => bgmActiveSource.Pause();
        public void ResumeBGM() => bgmActiveSource.UnPause();

        #endregion

        #region 音效

        public void PlaySFX(ResourceDefinitionDatabase.SoundResource sfx, float volumeScale = 1f)
        {
            if (!TryLoadSound(sfx, ResourceDefinitionDatabase.SoundCategory.SFX, out AudioClip clip))
                return;

            PlayClipImmediate(clip, sfxVolume * sfx.VolumeScale * volumeScale);
        }

        public void PlaySFXById(string sfxId, float volumeScale = 1f)
        {
            if (!ResourcesManager.TryGetSound(sfxId, out ResourceDefinitionDatabase.SoundResource sfx))
            {
                Debug.LogError($"[GameAudioManager] Cannot find SFX id: {sfxId}");
                return;
            }

            PlaySFX(sfx, volumeScale);
        }

        /// <summary>
        /// 直接播放 Resources 下的音效。路径相对 Assets/Resources，且不含扩展名。
        /// 适合尚未录入 ResourceDefinitionDatabase 的快速迭代音效。
        /// </summary>
        public void PlaySFXByResourcePath(string resourcePath, float volumeScale = 1f)
        {
            AudioClip clip = LoadSfxByResourcePath(resourcePath);
            if (clip == null)
                return;

            PlayClipImmediate(clip, sfxVolume * Mathf.Max(0f, volumeScale));
        }

        public void PlayRandomSFXByResourcePaths(float volumeScale, params string[] resourcePaths)
        {
            if (resourcePaths == null || resourcePaths.Length == 0)
                return;

            var availableClips = new List<AudioClip>(resourcePaths.Length);
            for (int i = 0; i < resourcePaths.Length; i++)
            {
                AudioClip clip = LoadSfxByResourcePath(resourcePaths[i]);
                if (clip != null)
                    availableClips.Add(clip);
            }

            if (availableClips.Count == 0)
                return;

            AudioClip selected = availableClips[Random.Range(0, availableClips.Count)];
            PlayClipImmediate(selected, sfxVolume * Mathf.Max(0f, volumeScale));
        }

        public void PlaySFXLoop(ResourceDefinitionDatabase.SoundResource sfx, float volumeScale = 1f)
        {
            if (!TryLoadSound(sfx, ResourceDefinitionDatabase.SoundCategory.SFX, out AudioClip clip))
                return;

            StopSFXById(sfx.Id);

            AudioSource source = CreateChildSource($"SFX_Loop_{sfx.Id}", loop: true);
            float finalVolumeScale = sfx.VolumeScale * volumeScale;
            source.clip = clip;
            source.volume = sfxVolume * finalVolumeScale;
            source.timeSamples = GetAudibleStartSample(clip);
            source.Play();

            loopingSfxSources[sfx.Id] = source;
            loopingSfxVolumeScales[sfx.Id] = finalVolumeScale;
        }

        public void PlaySFXLoopById(string sfxId, float volumeScale = 1f)
        {
            if (!ResourcesManager.TryGetSound(sfxId, out ResourceDefinitionDatabase.SoundResource sfx))
            {
                Debug.LogError($"[GameAudioManager] Cannot find SFX id: {sfxId}");
                return;
            }

            PlaySFXLoop(sfx, volumeScale);
        }

        public void PlaySFXLoopByResourcePath(string resourcePath, float volumeScale = 1f)
        {
            AudioClip clip = LoadSfxByResourcePath(resourcePath);
            if (clip == null)
                return;

            string loopKey = GetResourceLoopKey(resourcePath);
            StopSFXById(loopKey);

            AudioSource source = CreateChildSource($"SFX_Loop_{clip.name}", loop: true);
            float finalVolumeScale = Mathf.Max(0f, volumeScale);
            source.clip = clip;
            source.volume = sfxVolume * finalVolumeScale;
            source.timeSamples = GetAudibleStartSample(clip);
            source.Play();

            loopingSfxSources[loopKey] = source;
            loopingSfxVolumeScales[loopKey] = finalVolumeScale;
        }

        public void StopSFXLoopByResourcePath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return;

            StopSFXById(GetResourceLoopKey(resourcePath));
        }

        public void StopSFX(ResourceDefinitionDatabase.SoundResource sfx)
        {
            if (sfx == null)
            {
                Debug.LogWarning("[GameAudioManager] SFX resource is null.");
                return;
            }

            StopSFXById(sfx.Id);
        }

        public void StopSFXById(string sfxId)
        {
            if (string.IsNullOrWhiteSpace(sfxId))
            {
                Debug.LogWarning("[GameAudioManager] SFX id is empty.");
                return;
            }

            if (!loopingSfxSources.TryGetValue(sfxId, out AudioSource source))
                return;

            source.Stop();
            Destroy(source.gameObject);
            loopingSfxSources.Remove(sfxId);
            loopingSfxVolumeScales.Remove(sfxId);
        }

        public void StopAllSFX()
        {
            foreach (var pair in loopingSfxSources)
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);
            }

            loopingSfxSources.Clear();
            loopingSfxVolumeScales.Clear();
        }

        #endregion

        #region 音量

        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        #endregion

        #region 内部

        private AudioSource CreateChildSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private void PlayClipImmediate(AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            EnsureAudioDataLoaded(clip);
            AudioSource source = GetAvailableOneShotSource();
            source.Stop();
            source.clip = clip;
            source.loop = false;
            source.volume = Mathf.Max(0f, volume);
            source.timeSamples = GetAudibleStartSample(clip);
            source.Play();
        }

        private AudioSource GetAvailableOneShotSource()
        {
            for (int i = 0; i < oneShotSources.Count; i++)
            {
                AudioSource source = oneShotSources[i];
                if (source != null && !source.isPlaying)
                    return source;
            }

            AudioSource extraSource = CreateChildSource(
                $"SFX_OneShot_{oneShotSources.Count + 1}",
                loop: false);
            oneShotSources.Add(extraSource);
            return extraSource;
        }

        private AudioClip LoadSfxByResourcePath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                Debug.LogWarning("[GameAudioManager] SFX resource path is empty.");
                return null;
            }

            string normalizedPath = resourcePath.Trim();
            if (resourcePathClips.TryGetValue(normalizedPath, out AudioClip cachedClip))
            {
                EnsureAudioDataLoaded(cachedClip);
                return cachedClip;
            }

            AudioClip clip = Resources.Load<AudioClip>(normalizedPath);
            if (clip != null)
            {
                EnsureAudioDataLoaded(clip);
                resourcePathClips[normalizedPath] = clip;
                missingResourcePaths.Remove(normalizedPath);
                return clip;
            }

            if (missingResourcePaths.Add(normalizedPath))
                Debug.LogWarning($"[GameAudioManager] Missing SFX at Resources/{normalizedPath}");

            return null;
        }

        /// <summary>
        /// 启动时一次性加载 SFX，避免首次触发时才进行 Resources 查找和音频解码。
        /// </summary>
        private void PreloadResourceSfx()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(SfxResourceRoot);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null)
                    continue;

                EnsureAudioDataLoaded(clip);
                resourcePathClips[$"{SfxResourceRoot}/{clip.name}"] = clip;
                audibleStartSamples[clip] = FindAudibleStartSample(clip);
            }
        }

        private static void EnsureAudioDataLoaded(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
        }

        private int GetAudibleStartSample(AudioClip clip)
        {
            if (clip == null)
                return 0;

            if (audibleStartSamples.TryGetValue(clip, out int sample))
                return sample;

            sample = FindAudibleStartSample(clip);
            audibleStartSamples[clip] = sample;
            return sample;
        }

        private int FindAudibleStartSample(AudioClip clip)
        {
            if (clip == null
                || clip.samples <= 0
                || clip.channels <= 0
                || clip.loadType == AudioClipLoadType.Streaming)
            {
                return 0;
            }

            const int chunkFrames = 4096;
            int channels = clip.channels;
            float[] samples = new float[chunkFrames * channels];

            try
            {
                for (int frameOffset = 0; frameOffset < clip.samples; frameOffset += chunkFrames)
                {
                    int framesToRead = Mathf.Min(chunkFrames, clip.samples - frameOffset);
                    if (!clip.GetData(samples, frameOffset))
                        return 0;

                    int valuesToRead = framesToRead * channels;
                    for (int i = 0; i < valuesToRead; i++)
                    {
                        if (Mathf.Abs(samples[i]) < AudibleSampleThreshold)
                            continue;

                        int audibleFrame = frameOffset + (i / channels);
                        int preRollFrames = Mathf.RoundToInt(
                            clip.frequency * AudibleStartPreRollSeconds);
                        return Mathf.Max(0, audibleFrame - preRollFrames);
                    }
                }
            }
            catch (System.Exception exception)
            {
                if (unreadableAudioClips.Add(clip))
                {
                    Debug.LogWarning(
                        $"[GameAudioManager] Cannot inspect leading silence for '{clip.name}': {exception.Message}");
                }
            }

            return 0;
        }

        private static string GetResourceLoopKey(string resourcePath)
        {
            return $"resource:{resourcePath.Trim()}";
        }

        private void OnLevelCompleted(string _)
        {
            PlaySFXByResourcePath(GameSfxPaths.LevelCompleted);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                ApplySceneAmbience(scene.name);
        }

        private void ApplySceneAmbience(string sceneName)
        {
            StopSFXLoopByResourcePath(GameSfxPaths.FireplaceLoop);
            StopSFXLoopByResourcePath(GameSfxPaths.ClockTickLoop);

            if (string.Equals(sceneName, "Level1", System.StringComparison.Ordinal))
                PlaySFXLoopByResourcePath(GameSfxPaths.FireplaceLoop, 0.65f);
            else if (string.Equals(sceneName, "Level3", System.StringComparison.Ordinal))
                PlaySFXLoopByResourcePath(GameSfxPaths.ClockTickLoop, 0.55f);
        }

        private void ApplyVolumes()
        {
            float currentBgmVolume = bgmVolume * currentBgmVolumeScale;
            bgmSourceA.volume = currentBgmVolume;
            bgmSourceB.volume = currentBgmVolume;

            foreach (var pair in loopingSfxSources)
            {
                if (pair.Value == null)
                    continue;

                float volumeScale = loopingSfxVolumeScales.TryGetValue(pair.Key, out float scale) ? scale : 1f;
                pair.Value.volume = sfxVolume * volumeScale;
            }
        }

        private void PlayBgmImmediate(AudioClip clip, ResourceDefinitionDatabase.SoundResource music, string musicId)
        {
            currentBgmVolumeScale = music.VolumeScale;
            bgmActiveSource.clip = clip;
            bgmActiveSource.loop = music.Loop;
            bgmActiveSource.volume = bgmVolume * currentBgmVolumeScale;
            bgmActiveSource.Play();
            currentBgmId = musicId;
        }

        private void StopBgmFade()
        {
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }
        }

        /// <summary>解析切换时长：&lt;0 用默认 1.5/1.5，=0 立即，&gt;0 淡出与淡入同值。</summary>
        private void ResolveFadeDurations(float fadeDuration, out float fadeOut, out float fadeIn)
        {
            if (fadeDuration < 0f)
            {
                fadeOut = Mathf.Max(0f, defaultBgmFadeOutDuration);
                fadeIn = Mathf.Max(0f, defaultBgmFadeInDuration);
                return;
            }

            fadeOut = Mathf.Max(0f, fadeDuration);
            fadeIn = Mathf.Max(0f, fadeDuration);
        }

        /// <summary>先淡出旧曲，再淡入新曲；过程中广播 BgmSwitchProcess。</summary>
        private IEnumerator SwitchBgmRoutine(
            AudioClip nextClip,
            ResourceDefinitionDatabase.SoundResource music,
            string musicId,
            float fadeOutDuration,
            float fadeInDuration)
        {
            string fromId = currentBgmId ?? string.Empty;
            EmitBgmSwitch(fromId, musicId, BgmSwitchPhase.Started);

            AudioSource from = bgmActiveSource;
            AudioSource to = from == bgmSourceA ? bgmSourceB : bgmSourceA;
            float nextVolumeScale = music.VolumeScale;
            float nextVolume = bgmVolume * nextVolumeScale;

            // 1) 旧曲淡出
            fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
            if (from.isPlaying && fadeOutDuration > 0f)
            {
                float startVolume = from.volume;
                float elapsed = 0f;
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    from.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
                    yield return null;
                }
            }

            from.Stop();
            from.volume = 0f;
            EmitBgmSwitch(fromId, musicId, BgmSwitchPhase.FadeOutCompleted);

            // 2) 新曲淡入
            to.clip = nextClip;
            to.loop = music.Loop;
            to.volume = 0f;
            to.Play();

            fadeInDuration = Mathf.Max(0f, fadeInDuration);
            if (fadeInDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeInDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    to.volume = Mathf.Lerp(0f, nextVolume, elapsed / fadeInDuration);
                    yield return null;
                }
            }

            to.volume = nextVolume;
            bgmActiveSource = to;
            currentBgmId = musicId;
            currentBgmVolumeScale = nextVolumeScale;
            bgmFadeCoroutine = null;

            EmitBgmSwitch(fromId, musicId, BgmSwitchPhase.Completed);
        }

        private static void EmitBgmSwitch(string fromId, string toId, BgmSwitchPhase phase)
        {
            EventManager.Emit(GameEvents.BgmSwitchProcess, new BgmSwitchInfo(fromId, toId, phase));
        }

        private IEnumerator FadeOutAndStopBgmRoutine(float duration)
        {
            AudioSource from = bgmActiveSource;
            float startVolume = from.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                from.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            bgmSourceA.Stop();
            bgmSourceB.Stop();
            currentBgmId = null;
            currentBgmVolumeScale = 1f;
            ApplyVolumes();
            bgmFadeCoroutine = null;
        }

        private bool TryLoadSound(ResourceDefinitionDatabase.SoundResource sound, ResourceDefinitionDatabase.SoundCategory expectedCategory, out AudioClip clip)
        {
            clip = null;
            if (sound == null)
            {
                Debug.LogWarning("[GameAudioManager] Sound resource is null.");
                return false;
            }

            if (sound.Category != expectedCategory)
            {
                Debug.LogWarning($"[GameAudioManager] Sound '{sound.Id}' is {sound.Category}, expected {expectedCategory}.");
                return false;
            }

            clip = ResourcesManager.LoadSound(sound);
            return clip != null;
        }

        #endregion
    }

    /// <summary>已通过音效在 Resources/Audio/SFX 下的统一路径。</summary>
    public static class GameSfxPaths
    {
        private const string Root = "Audio/SFX/";

        public const string WorldSwitch2 = Root + "切换2";
        public const string WorldSwitch4 = Root + "切换4";
        public const string InteractionConfirm = Root + "交互-获得或收取成功-v2";
        public const string PuzzleSuccess = Root + "提示-顺序正确";
        public const string PuzzleFailure1 = Root + "切换1";
        public const string PuzzleFailure3 = Root + "切换3";
        public const string UiHover = Root + "交互-鼠标悬停";
        public const string UiConfirm = Root + "交互-点击";
        public const string UiBack = Root + "返回";
        public const string PauseToggle = Root + "暂停";
        public const string LevelCompleted = Root + "关卡完成-找到本关记忆";
        public const string FireplaceLoop = Root + "木材在火炉里燃烧_耳聆网_[声音ID：10744]";
        public const string LightSwitch = Root + "开灯-拉绳灯";
        public const string ClockTickLoop = Root + "钟表-滴答滴答";
        public const string ClockHourBell = Root + "完成谜题-钟声到整点提示音";
    }
}
