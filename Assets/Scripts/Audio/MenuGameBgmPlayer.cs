using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Anchor.Audio
{
    [DisallowMultipleComponent]
    public sealed class MenuGameBgmPlayer : MonoBehaviour
    {
        public static MenuGameBgmPlayer Instance { get; private set; }

        private const string MenuClipPath = "Assets/ArtRes/前30s.mp3";
        private const string GameClipPath = "Assets/ArtRes/后.mp3";

        [Header("Clips")]
        [SerializeField] private AudioClip menuClip;
        [SerializeField] private AudioClip gameClip;

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Min(0f)] private float fadeDuration = 1.5f;
        [SerializeField] private bool playMenuOnStart = true;

        private AudioSource primarySource;
        private AudioSource secondarySource;
        private Coroutine fadeRoutine;
        private Coroutine loopRoutine;
        private bool primaryIsActive = true;
        private AudioClip currentClip;

        public static void PlayMenuMusic()
        {
            GetOrCreate().PlayMenu();
        }

        public static void PlayGameMusic()
        {
            GetOrCreate().PlayGame();
        }

        public static MenuGameBgmPlayer GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            MenuGameBgmPlayer player = FindObjectOfType<MenuGameBgmPlayer>(true);
            if (player != null)
            {
                return player;
            }

            var playerObject = new GameObject(nameof(MenuGameBgmPlayer));
            return playerObject.AddComponent<MenuGameBgmPlayer>();
        }

        private void Awake()
        {
            if (!RegisterInstance())
            {
                enabled = false;
                return;
            }

            AutoAssignEditorClips();
            EnsureSources();
        }

        private void Start()
        {
            if (playMenuOnStart)
            {
                PlayMenu(0f);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void PlayMenu()
        {
            PlayMenu(fadeDuration);
        }

        public void PlayGame()
        {
            PlayGame(fadeDuration);
        }

        private void PlayMenu(float duration)
        {
            PlayClip(menuClip, duration, MenuClipPath);
        }

        private void PlayGame(float duration)
        {
            PlayClip(gameClip, duration, GameClipPath);
        }

        private bool RegisterInstance()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"{nameof(MenuGameBgmPlayer)} already exists. Disabling duplicate: {name}", this);
                return false;
            }

            Instance = this;
            return true;
        }

        private void EnsureSources()
        {
            primarySource = EnsureSource(primarySource, "Primary");
            secondarySource = EnsureSource(secondarySource, "Secondary");
        }

        private AudioSource EnsureSource(AudioSource source, string sourceName)
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.name = sourceName;
            return source;
        }

        private void PlayClip(AudioClip clip, float duration, string assetPath)
        {
            if (clip == null)
            {
                Debug.LogWarning($"{nameof(MenuGameBgmPlayer)} cannot find BGM clip at {assetPath}.", this);
                return;
            }

            EnsureSources();

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            StopLoopRoutine();

            AudioSource activeSource = primaryIsActive ? primarySource : secondarySource;

            if (currentClip == clip && activeSource.isPlaying)
            {
                fadeRoutine = StartCoroutine(FadeSingleSource(activeSource, duration, true));
                return;
            }

            AudioSource nextSource = primaryIsActive ? secondarySource : primarySource;

            if (duration <= 0f || currentClip == null)
            {
                PlayImmediate(activeSource, nextSource, clip);
                return;
            }

            fadeRoutine = StartCoroutine(CrossFade(activeSource, nextSource, clip, duration));
        }

        private void PlayImmediate(AudioSource activeSource, AudioSource nextSource, AudioClip clip)
        {
            activeSource.Stop();
            activeSource.volume = 0f;

            nextSource.clip = clip;
            nextSource.volume = volume;
            nextSource.Play();

            primaryIsActive = nextSource == primarySource;
            currentClip = clip;
            StartLoopRoutine();
        }

        private IEnumerator CrossFade(AudioSource fromSource, AudioSource toSource, AudioClip clip, float duration)
        {
            float fromStartVolume = fromSource.isPlaying ? fromSource.volume : 0f;
            toSource.clip = clip;
            toSource.volume = 0f;
            toSource.Play();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fromSource.volume = Mathf.Lerp(fromStartVolume, 0f, t);
                toSource.volume = Mathf.Lerp(0f, volume, t);
                yield return null;
            }

            fromSource.Stop();
            fromSource.volume = 0f;
            toSource.volume = volume;

            primaryIsActive = toSource == primarySource;
            currentClip = clip;
            fadeRoutine = null;
            StartLoopRoutine();
        }

        private IEnumerator FadeSingleSource(AudioSource source, float duration, bool restartLoopWhenDone)
        {
            if (duration <= 0f)
            {
                source.volume = volume;
                fadeRoutine = null;
                if (restartLoopWhenDone)
                {
                    StartLoopRoutine();
                }

                yield break;
            }

            float startVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, volume, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            source.volume = volume;
            fadeRoutine = null;

            if (restartLoopWhenDone)
            {
                StartLoopRoutine();
            }
        }

        private void StartLoopRoutine()
        {
            StopLoopRoutine();

            AudioSource activeSource = primaryIsActive ? primarySource : secondarySource;
            if (activeSource != null && activeSource.clip != null)
            {
                loopRoutine = StartCoroutine(LoopWithFade(activeSource));
            }
        }

        private void StopLoopRoutine()
        {
            if (loopRoutine != null)
            {
                StopCoroutine(loopRoutine);
                loopRoutine = null;
            }
        }

        private IEnumerator LoopWithFade(AudioSource activeSource)
        {
            while (activeSource != null && activeSource.clip != null)
            {
                AudioClip clip = activeSource.clip;
                float waitSeconds = Mathf.Max(0f, clip.length - Mathf.Max(0f, fadeDuration));
                yield return new WaitForSecondsRealtime(waitSeconds);

                AudioSource nextSource = activeSource == primarySource ? secondarySource : primarySource;
                yield return CrossFadeLoop(activeSource, nextSource, clip, fadeDuration);

                activeSource = nextSource;
            }
        }

        private IEnumerator CrossFadeLoop(AudioSource fromSource, AudioSource toSource, AudioClip clip, float duration)
        {
            float fromStartVolume = fromSource.isPlaying ? fromSource.volume : 0f;
            toSource.clip = clip;
            toSource.volume = 0f;
            toSource.time = 0f;
            toSource.Play();

            if (duration <= 0f)
            {
                fromSource.Stop();
                fromSource.volume = 0f;
                toSource.volume = volume;
                primaryIsActive = toSource == primarySource;
                currentClip = clip;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fromSource.volume = Mathf.Lerp(fromStartVolume, 0f, t);
                toSource.volume = Mathf.Lerp(0f, volume, t);
                yield return null;
            }

            fromSource.Stop();
            fromSource.volume = 0f;
            toSource.volume = volume;

            primaryIsActive = toSource == primarySource;
            currentClip = clip;
        }

        private void AutoAssignEditorClips()
        {
#if UNITY_EDITOR
            if (menuClip == null)
            {
                menuClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MenuClipPath);
            }

            if (gameClip == null)
            {
                gameClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GameClipPath);
            }
#endif
        }

        private void OnValidate()
        {
            AutoAssignEditorClips();
        }
    }
}
