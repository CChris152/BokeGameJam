using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Core
{
    /// <summary>
    /// Global audio manager. Resource loading is delegated to ResourcesManager.
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

        private string currentBgmName;
        private Coroutine bgmFadeCoroutine;

        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;
        public string CurrentBgmName => currentBgmName;

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

        public void PlayBGM(string musicName, bool loop = true, float fadeDuration = -1f)
        {
            AudioClip clip = ResourcesManager.LoadMusic(musicName);
            if (clip == null)
                return;

            if (currentBgmName == musicName && bgmActiveSource.isPlaying)
                return;

            float fade = fadeDuration < 0f ? defaultBgmFadeDuration : fadeDuration;

            if (string.IsNullOrEmpty(currentBgmName) || fade <= 0f)
            {
                StopBgmFade();
                PlayBgmImmediate(clip, musicName, loop);
                return;
            }

            SwitchBGM(musicName, fade, loop);
        }

        public void SwitchBGM(string musicName, float fadeDuration = -1f, bool loop = true)
        {
            AudioClip clip = ResourcesManager.LoadMusic(musicName);
            if (clip == null)
                return;

            if (currentBgmName == musicName && bgmActiveSource.isPlaying)
                return;

            float fade = fadeDuration < 0f ? defaultBgmFadeDuration : fadeDuration;
            StopBgmFade();
            bgmFadeCoroutine = StartCoroutine(CrossfadeBgmRoutine(clip, musicName, loop, fade));
        }

        public void StopBGM(float fadeDuration = 0f)
        {
            if (fadeDuration <= 0f)
            {
                StopBgmFade();
                bgmSourceA.Stop();
                bgmSourceB.Stop();
                currentBgmName = null;
                return;
            }

            StopBgmFade();
            bgmFadeCoroutine = StartCoroutine(FadeOutAndStopBgmRoutine(fadeDuration));
        }

        public void PauseBGM() => bgmActiveSource.Pause();
        public void ResumeBGM() => bgmActiveSource.UnPause();

        #endregion

        #region SFX

        public void PlaySFX(string sfxName, float volumeScale = 1f)
        {
            AudioClip clip = ResourcesManager.LoadSound(sfxName);
            if (clip == null)
                return;

            sfxOneShotSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        public void PlaySFXLoop(string sfxName, float volumeScale = 1f)
        {
            AudioClip clip = ResourcesManager.LoadSound(sfxName);
            if (clip == null)
                return;

            if (loopingSfxSources.TryGetValue(sfxName, out AudioSource existing))
            {
                existing.Stop();
                Destroy(existing.gameObject);
                loopingSfxSources.Remove(sfxName);
            }

            AudioSource source = CreateChildSource($"SFX_Loop_{sfxName}", loop: true);
            source.clip = clip;
            source.volume = sfxVolume * volumeScale;
            source.Play();
            loopingSfxSources[sfxName] = source;
        }

        public void StopSFX(string sfxName)
        {
            if (!loopingSfxSources.TryGetValue(sfxName, out AudioSource source))
                return;

            source.Stop();
            Destroy(source.gameObject);
            loopingSfxSources.Remove(sfxName);
        }

        public void StopAllSFX()
        {
            foreach (var pair in loopingSfxSources)
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);
            }

            loopingSfxSources.Clear();
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
            bgmSourceA.volume = bgmVolume;
            bgmSourceB.volume = bgmVolume;

            foreach (var pair in loopingSfxSources)
            {
                if (pair.Value != null)
                    pair.Value.volume = sfxVolume;
            }
        }

        private void PlayBgmImmediate(AudioClip clip, string musicName, bool loop)
        {
            bgmActiveSource.clip = clip;
            bgmActiveSource.loop = loop;
            bgmActiveSource.volume = bgmVolume;
            bgmActiveSource.Play();
            currentBgmName = musicName;
        }

        private void StopBgmFade()
        {
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }
        }

        private IEnumerator CrossfadeBgmRoutine(AudioClip nextClip, string musicName, bool loop, float duration)
        {
            AudioSource from = bgmActiveSource;
            AudioSource to = from == bgmSourceA ? bgmSourceB : bgmSourceA;

            to.clip = nextClip;
            to.loop = loop;
            to.volume = 0f;
            to.Play();

            float elapsed = 0f;
            float fromStartVolume = bgmVolume;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                from.volume = Mathf.Lerp(fromStartVolume, 0f, t);
                to.volume = Mathf.Lerp(0f, bgmVolume, t);
                yield return null;
            }

            from.Stop();
            from.volume = bgmVolume;
            to.volume = bgmVolume;

            bgmActiveSource = to;
            currentBgmName = musicName;
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
            bgmSourceA.volume = bgmVolume;
            bgmSourceB.volume = bgmVolume;
            currentBgmName = null;
            bgmFadeCoroutine = null;
        }

        #endregion
    }
}
