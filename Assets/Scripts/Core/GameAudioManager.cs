using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Core
{
    /// <summary>
    /// 全局音频管理器。音频资源通过 ResourcesManager 解析。
    /// </summary>
    public class GameAudioManager : MonoBehaviour
    {
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
        private AudioSource sfxOneShotSource;

        private readonly Dictionary<string, AudioSource> loopingSfxSources = new();
        private readonly Dictionary<string, float> loopingSfxVolumeScales = new();

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
            sfxOneShotSource = CreateChildSource("SFX_OneShot", loop: false);

            bgmActiveSource = bgmSourceA;
            ApplyVolumes();
        }

        private void Start()
        {
            // 启动时从 DataManager 恢复已保存的主音量
            LoadVolumesFromDataManager();
        }

        /// <summary>
        /// 从 DataManager 读取共用主音量（MasterVolume），并同时应用到 BGM 与 SFX。
        /// </summary>
        public void LoadVolumesFromDataManager()
        {
            if (DataManager.Instance == null)
                return;

            float masterVolume = Mathf.Clamp01(DataManager.Instance.GetFloat(DataManager.Keys.MasterVolume));
            SetBGMVolume(masterVolume);
            SetSFXVolume(masterVolume);
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

            sfxOneShotSource.PlayOneShot(clip, sfxVolume * sfx.VolumeScale * volumeScale);
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

        public void PlaySFXLoop(ResourceDefinitionDatabase.SoundResource sfx, float volumeScale = 1f)
        {
            if (!TryLoadSound(sfx, ResourceDefinitionDatabase.SoundCategory.SFX, out AudioClip clip))
                return;

            StopSFXById(sfx.Id);

            AudioSource source = CreateChildSource($"SFX_Loop_{sfx.Id}", loop: true);
            float finalVolumeScale = sfx.VolumeScale * volumeScale;
            source.clip = clip;
            source.volume = sfxVolume * finalVolumeScale;
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
}
