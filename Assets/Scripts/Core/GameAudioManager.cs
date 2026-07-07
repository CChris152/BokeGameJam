using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Core
{
    /// <summary>
    /// Global audio manager. Audio assets are resolved through ResourcesManager.
    /// </summary>
    public class GameAudioManager : MonoBehaviour
    {
        public static GameAudioManager Instance { get; private set; }

        [Header("Volume")]
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        [Header("BGM Switch")]
        [SerializeField] private float defaultBgmFadeDuration = 0.5f;

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

        #region BGM

        public void PlayBGM(ResourceDefinitionDatabase.SoundResource music, float fadeDuration = -1f)
        {
            if (!TryLoadSound(music, ResourceDefinitionDatabase.SoundCategory.Music, out AudioClip clip))
                return;

            string musicId = music.Id;
            if (currentBgmId == musicId && bgmActiveSource.isPlaying)
                return;

            float fade = fadeDuration < 0f ? defaultBgmFadeDuration : fadeDuration;

            if (string.IsNullOrEmpty(currentBgmId) || fade <= 0f)
            {
                StopBgmFade();
                PlayBgmImmediate(clip, music, musicId);
                return;
            }

            SwitchBGM(music, fade);
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

        public void SwitchBGM(ResourceDefinitionDatabase.SoundResource music, float fadeDuration = -1f)
        {
            if (!TryLoadSound(music, ResourceDefinitionDatabase.SoundCategory.Music, out AudioClip clip))
                return;

            string musicId = music.Id;
            if (currentBgmId == musicId && bgmActiveSource.isPlaying)
                return;

            float fade = fadeDuration < 0f ? defaultBgmFadeDuration : fadeDuration;
            StopBgmFade();
            bgmFadeCoroutine = StartCoroutine(CrossfadeBgmRoutine(clip, music, musicId, fade));
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

        #region SFX

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

        #region Volume

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

        #region Internal

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

        private IEnumerator CrossfadeBgmRoutine(AudioClip nextClip, ResourceDefinitionDatabase.SoundResource music, string musicId, float duration)
        {
            AudioSource from = bgmActiveSource;
            AudioSource to = from == bgmSourceA ? bgmSourceB : bgmSourceA;
            float nextVolumeScale = music.VolumeScale;
            float nextVolume = bgmVolume * nextVolumeScale;

            to.clip = nextClip;
            to.loop = music.Loop;
            to.volume = 0f;
            to.Play();

            float elapsed = 0f;
            float fromStartVolume = from.volume;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                from.volume = Mathf.Lerp(fromStartVolume, 0f, t);
                to.volume = Mathf.Lerp(0f, nextVolume, t);
                yield return null;
            }

            from.Stop();
            from.volume = nextVolume;
            to.volume = nextVolume;

            bgmActiveSource = to;
            currentBgmId = musicId;
            currentBgmVolumeScale = nextVolumeScale;
            bgmFadeCoroutine = null;
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
