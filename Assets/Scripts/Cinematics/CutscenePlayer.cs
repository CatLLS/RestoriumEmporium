// ============================================================
// CutscenePlayer — full-screen video overlay for the Game scene.
// WHAT & WHY: Implements ICutscenePlayer. Batch 2 plays three videos: Tracy's
//   intro (tracysc1) on first launch, her reaction after poster 1 (tracysc2)
//   and the book opening (openBookTransition) on the way from the desk hub to
//   the journal. The flow only wants "play this, tell me when it is over"; this
//   component does everything else — texture, aspect, fade, input blocking,
//   music, timeouts — and guarantees the "tell me" part happens exactly once.
// KEY DECISIONS:
//   - VideoPlayer renders into a RenderTexture created AT RUNTIME at the clip's
//     own pixel size and destroyed after the fade-out. Nothing is allocated
//     while no video plays, and there is no project asset to keep in sync with
//     the clip resolution.
//   - The RawImage uses an AspectRatioFitter in Envelope Parent mode: the video
//     always fills the whole 412 x 917 portrait screen and any excess is cropped
//     off the edges, never letterboxed. A black Backdrop behind it covers the
//     frames before the first video frame arrives.
//   - onFinished fires EXACTLY once per Play(): on the clip's natural end, on a
//     skip tap (only when skippable), on a VideoPlayer error, on a prepare that
//     takes longer than Prepare Timeout Seconds, or on a safety timeout of clip
//     length + Timeout Padding Seconds. A broken or stalled video can therefore
//     never soft-lock the game. The one case it is dropped is this component
//     being DESTROYED (the whole scene is going away, nobody is left to tell).
//   - onFinished is called at the START of the fade-out, while the overlay still
//     covers the screen. The flow swaps the screen underneath, then the video
//     fades away to reveal it — a crossfade instead of a flash of the old screen.
//     IsPlaying (and PlayingChanged(false)) only drops once the fade is done, so
//     the tutorial does not start talking over the last frames.
//   - Timeouts count UNSCALED time clamped to 0.25 s per frame. Returning from
//     the Android home screen produces one huge delta; clamping it means a
//     backgrounded app does not come back to a video that "timed out" while the
//     phone was in the player's pocket.
//   - Input: the CanvasGroup blocks raycasts from the first frame of Play() to
//     the end of the fade-out, so nothing underneath can be tapped. The skip tap
//     is an IPointerClickHandler (the uGUI EventSystem, which already runs on the
//     new Input System), ignored for the first Min Seconds Before Skip.
//   - Audio: the videos carry their own soundtrack. It is routed through an
//     AudioSource (VideoAudioOutputMode.AudioSource) whose output is the
//     MainMixer "Music" group, and the game's music is PAUSED (IAudioService.
//     SetMusicPaused) while a video plays. A cutscene's audio is a long scored
//     bed, the same kind of sound as the music, so the Music slider is the
//     control a player expects to affect it; the SFX slider stays for short UI
//     and tool feedback. If the AudioSource has no mixer group assigned, the
//     music group is borrowed from AudioManager; if even that is missing, the
//     source's volume is set from SaveData.musicVolume instead.
//   - A second Play() while a video is running is a flow bug: it is logged and
//     its own onFinished fires immediately (so that caller cannot hang), and the
//     running video carries on. A Play() during the fade-out of the previous
//     one is fine and simply takes over the overlay.
//   - An optional nested Canvas on this object is disabled while idle, so the
//     overlay costs nothing to draw or raycast when no video is playing.
//   - Registered as ICutscenePlayer in ServiceLocator in Awake (so the flow's
//     Start can already find it) and unregistered in OnDestroy.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open the Game scene. Right-click the main Canvas -> Create Empty, name it
//     exactly "CutscenePlayer". Drag it to the BOTTOM of the Canvas's children
//     so it draws on top of every screen and overlay.
// [ ] Make its RectTransform fill the screen: in the Rect Transform, click the
//     anchor preset square, hold Alt+Shift and click the bottom-right "stretch"
//     option. Left/Top/Right/Bottom should all read 0.
// [ ] Add Component on "CutscenePlayer", one at a time: "Canvas" (tick Override
//     Sorting, set Sort Order to 1000), "Graphic Raycaster", "Canvas Group",
//     "Video Player", "Audio Source", and finally "Cutscene Player".
// [ ] On the Audio Source: untick "Play On Awake"; set Output to MainMixer ->
//     Music (click the small circle next to Output and pick "Music").
// [ ] On the Video Player: leave Source = Video Clip and the Video Clip slot
//     EMPTY (the script sets it), untick "Play On Awake" and "Loop".
// [ ] Right-click "CutscenePlayer" -> UI -> Image, name it "Backdrop". Stretch it
//     to fill (same Alt+Shift trick). Set its Color to pure black, alpha 255.
// [ ] Right-click "CutscenePlayer" -> UI -> Raw Image, name it "VideoSurface".
//     Add Component -> "Aspect Ratio Fitter"; set Aspect Mode = Envelope Parent.
//     Leave Texture empty.
// [ ] Optional skip hint: right-click "CutscenePlayer" -> UI -> Text -
//     TextMeshPro, name it "SkipHint", anchor it bottom-centre, add a
//     "Localized Text" component with key ui.cutscene.skipHint. Untick the
//     SkipHint object (the script shows it only on skippable videos).
// [ ] Select "CutscenePlayer" and wire the Cutscene Player fields:
//       Video Surface       <- the "VideoSurface" child
//       Video Player        <- "CutscenePlayer" itself
//       Group               <- "CutscenePlayer" itself (its Canvas Group)
//       Fitter              <- the "VideoSurface" child (its Aspect Ratio Fitter)
//       Skip Hint           <- the "SkipHint" child (or leave empty)
//       Video Audio Source  <- "CutscenePlayer" itself (its Audio Source)
//       Overlay Canvas      <- "CutscenePlayer" itself (its Canvas)
// [ ] Leave the CutscenePlayer object ACTIVE (ticked). It hides itself; if it
//     is inactive it never registers and every video is silently skipped.
// [ ] The clips themselves are NOT assigned here. GameFlowController has the
//     intro and book clips; each PosterData has its own completion clip.
// ---------------------------------------------------------------

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RestoriumEmporium.Cinematics
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Core;

    [DisallowMultipleComponent]
    public class CutscenePlayer : MonoBehaviour, ICutscenePlayer, IPointerClickHandler
    {
        // One backgrounded-app delta must not eat a timeout.
        private const float MaxTimeStep = 0.25f;

        [Header("Scene wiring")]
        [Tooltip("Full-screen RawImage the video is drawn into (child 'VideoSurface').")]
        [SerializeField] private RawImage videoSurface;

        [Tooltip("The VideoPlayer that decodes the clip. Usually on this same object.")]
        [SerializeField] private VideoPlayer videoPlayer;

        [Tooltip("CanvasGroup on this object: fades the overlay and blocks taps while playing.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("AspectRatioFitter on the VideoSurface, Aspect Mode = Envelope Parent.")]
        [SerializeField] private AspectRatioFitter fitter;

        [Tooltip("Optional 'Tap to skip' label. Shown only on skippable videos.")]
        [SerializeField] private TMP_Text skipHint;

        [Tooltip("AudioSource the video's soundtrack plays through. Set its Output to " +
                 "MainMixer/Music. Added automatically if left empty.")]
        [SerializeField] private AudioSource videoAudioSource;

        [Tooltip("Optional nested Canvas on this object. Disabled while idle so the " +
                 "overlay costs nothing to draw.")]
        [SerializeField] private Canvas overlayCanvas;

        [Header("Timing")]
        [Tooltip("Seconds to fade the overlay in and out.")]
        [Range(0f, 2f)]
        [SerializeField] private float fadeSeconds = 0.25f;

        [Tooltip("Safety net: a video still 'playing' this long after its own length ends anyway.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float timeoutPaddingSeconds = 3f;

        [Tooltip("Safety net: give up if the clip has not finished preparing after this long.")]
        [Range(1f, 30f)]
        [SerializeField] private float prepareTimeoutSeconds = 10f;

        [Tooltip("Taps are ignored for this long after a skippable video starts, so the " +
                 "tap that opened it cannot also skip it.")]
        [Range(0f, 3f)]
        [SerializeField] private float minSecondsBeforeSkip = 0.5f;

        [Tooltip("Seconds into a skippable video before the skip hint appears.")]
        [Range(0f, 5f)]
        [SerializeField] private float skipHintDelaySeconds = 1f;

        private enum Phase
        {
            Idle,
            Preparing,
            Playing,
            Ending
        }

        private Phase _phase = Phase.Idle;
        private Action _onFinished;
        private bool _skippable;
        private float _elapsed;
        private RenderTexture _texture;
        private Coroutine _routine;
        private IAudioService _audio;
        private bool _musicHeld;

        /// <inheritdoc />
        public bool IsPlaying => _phase != Phase.Idle;

        /// <inheritdoc />
        public event Action<bool> PlayingChanged;

        private void Awake()
        {
            ResolveReferences();
            ConfigureVideoPlayer();
            ShowIdle();

            ServiceLocator.Register<ICutscenePlayer>(this);
        }

        private void OnDisable()
        {
            if (_phase == Phase.Preparing || _phase == Phase.Playing)
            {
                // Something deactivated the overlay mid-video. Still honour the
                // exactly-once promise; no coroutine can run now, so end at once.
                StopVideo();
                var callback = TakeCallback();
                EndImmediately(raiseChanged: true);
                InvokeSafely(callback);
                return;
            }

            if (_phase == Phase.Ending)
            {
                EndImmediately(raiseChanged: true);
            }
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnLoopPointReached;
                videoPlayer.errorReceived -= OnErrorReceived;
            }

            // The scene is going away: nobody is left to receive onFinished.
            _onFinished = null;
            ReleaseMusic();
            ReleaseTexture();

            ServiceLocator.Unregister<ICutscenePlayer>(this);
        }

        // ---- ICutscenePlayer --------------------------------------------------------

        /// <inheritdoc />
        public void Play(VideoClip clip, bool skippable, Action onFinished)
        {
            if (clip == null)
            {
                onFinished?.Invoke();
                return;
            }

            if (_phase == Phase.Preparing || _phase == Phase.Playing)
            {
                Debug.LogWarning($"[CutscenePlayer] Play('{clip.name}') while another video is " +
                                 "playing. Skipping the new one so its caller does not hang.", this);
                onFinished?.Invoke();
                return;
            }

            if (!isActiveAndEnabled || videoPlayer == null || videoSurface == null)
            {
                Debug.LogWarning($"[CutscenePlayer] Cannot play '{clip.name}': the CutscenePlayer " +
                                 "object is inactive or its Video Player / Video Surface fields are " +
                                 "empty. Skipping the video.", this);
                onFinished?.Invoke();
                return;
            }

            var wasIdle = _phase == Phase.Idle;

            if (_phase == Phase.Ending)
            {
                // The previous video is fading out; take the overlay over.
                StopRoutine();
                ReleaseTexture();
            }

            _onFinished = onFinished;
            _skippable = skippable;
            _elapsed = 0f;

            CreateTexture(clip);
            ConfigureAudio(clip);
            videoPlayer.clip = clip;

            HoldMusic();
            ShowActive();

            _phase = Phase.Preparing;

            if (wasIdle)
            {
                PlayingChanged?.Invoke(true);
            }

            // The routine starts BEFORE Prepare(): a clip that fails synchronously
            // raises errorReceived inside Prepare(), and Finish() must then find
            // (and stop) this routine rather than have it started afterwards.
            _routine = StartCoroutine(RunRoutine(clip.length));
            videoPlayer.Prepare();
        }

        // ---- Input ------------------------------------------------------------------

        /// <summary>A tap anywhere on the overlay. Skips when the video allows it.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_skippable || _elapsed < minSecondsBeforeSkip)
            {
                return;
            }

            if (_phase == Phase.Preparing || _phase == Phase.Playing)
            {
                Finish();
            }
        }

        // ---- Playback ---------------------------------------------------------------

        private IEnumerator RunRoutine(double clipLength)
        {
            var startAlpha = group != null ? group.alpha : 1f;
            var fadeTime = 0f;
            var preparingTime = 0f;
            var playingTime = 0f;
            var limit = (float)clipLength + timeoutPaddingSeconds;

            while (_phase == Phase.Preparing || _phase == Phase.Playing)
            {
                var dt = Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
                _elapsed += dt;

                if (group != null && group.alpha < 1f)
                {
                    fadeTime += dt;
                    group.alpha = fadeSeconds <= 0f ? 1f : Mathf.Lerp(startAlpha, 1f, fadeTime / fadeSeconds);
                }

                if (_phase == Phase.Preparing)
                {
                    preparingTime += dt;

                    if (videoPlayer.isPrepared)
                    {
                        BeginPlayback();
                    }
                    else if (preparingTime > prepareTimeoutSeconds)
                    {
                        Debug.LogWarning($"[CutscenePlayer] '{NameOfClip()}' did not prepare within " +
                                         $"{prepareTimeoutSeconds:0.#}s. Skipping it.", this);
                        Finish();
                        yield break;
                    }
                }
                else
                {
                    playingTime += dt;

                    if (_skippable && skipHint != null && playingTime >= skipHintDelaySeconds &&
                        !skipHint.gameObject.activeSelf)
                    {
                        skipHint.gameObject.SetActive(true);
                    }

                    if (playingTime > limit)
                    {
                        Debug.LogWarning($"[CutscenePlayer] '{NameOfClip()}' ran past its length + " +
                                         $"{timeoutPaddingSeconds:0.#}s. Ending it.", this);
                        Finish();
                        yield break;
                    }
                }

                yield return null;
            }
        }

        private void BeginPlayback()
        {
            _phase = Phase.Playing;

            // The decoded size can differ from the importer's metadata (a
            // transcode setting); trust what the decoder reports.
            if (fitter != null && videoPlayer.width > 0 && videoPlayer.height > 0)
            {
                fitter.aspectRatio = videoPlayer.width / (float)videoPlayer.height;
            }

            videoPlayer.Play();
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            if (_phase == Phase.Playing)
            {
                Finish();
            }
        }

        private void OnErrorReceived(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[CutscenePlayer] Video error on '{NameOfClip()}': {message}. " +
                             "Skipping the video.", this);

            if (_phase == Phase.Preparing || _phase == Phase.Playing)
            {
                Finish();
            }
        }

        /// <summary>The single exit for a running video. Callback, then fade-out.</summary>
        private void Finish()
        {
            if (_phase != Phase.Preparing && _phase != Phase.Playing)
            {
                return;
            }

            StopRoutine();
            StopVideo();

            if (skipHint != null)
            {
                skipHint.gameObject.SetActive(false);
            }

            var callback = TakeCallback();
            _phase = Phase.Ending;
            ReleaseMusic();

            if (group != null)
            {
                group.interactable = false;
            }

            _routine = StartCoroutine(FadeOutRoutine());

            // Last, so a Play() from inside the callback finds a consistent
            // "fading out" state and can take the overlay over.
            InvokeSafely(callback);
        }

        private IEnumerator FadeOutRoutine()
        {
            var startAlpha = group != null ? group.alpha : 0f;
            var t = 0f;

            while (group != null && fadeSeconds > 0f && t < fadeSeconds)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
                group.alpha = Mathf.Lerp(startAlpha, 0f, t / fadeSeconds);
                yield return null;
            }

            _routine = null;
            EndImmediately(raiseChanged: true);
        }

        private void EndImmediately(bool raiseChanged)
        {
            StopRoutine();
            ReleaseMusic();
            ReleaseTexture();
            ShowIdle();

            var wasActive = _phase != Phase.Idle;
            _phase = Phase.Idle;

            if (raiseChanged && wasActive)
            {
                PlayingChanged?.Invoke(false);
            }
        }

        private void StopVideo()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.Stop();

            // Drop the clip reference so the decoder's buffers can be released.
            videoPlayer.clip = null;
        }

        private void StopRoutine()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private Action TakeCallback()
        {
            var callback = _onFinished;
            _onFinished = null;
            return callback;
        }

        private void InvokeSafely(Action callback)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback();
            }
            catch (Exception e)
            {
                // A throwing listener must not leave the overlay stuck on screen.
                Debug.LogException(e, this);
            }
        }

        // ---- Texture ----------------------------------------------------------------

        private void CreateTexture(VideoClip clip)
        {
            ReleaseTexture();

            var width = (int)clip.width;
            var height = (int)clip.height;

            if (width <= 0 || height <= 0)
            {
                // Metadata missing; portrait HD is the likeliest shape and the
                // fitter is corrected from the decoder once playback starts.
                width = 1080;
                height = 1920;
            }

            _texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "CutsceneRT_" + clip.name,
                useMipMap = false,
                autoGenerateMips = false
            };
            _texture.Create();

            // A fresh RenderTexture holds garbage on some GPUs; start from black.
            var previous = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;

            videoPlayer.targetTexture = _texture;
            videoSurface.texture = _texture;

            if (fitter != null)
            {
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = width / (float)height;
            }
        }

        private void ReleaseTexture()
        {
            if (videoPlayer != null)
            {
                videoPlayer.targetTexture = null;
            }

            if (videoSurface != null)
            {
                videoSurface.texture = null;
            }

            if (_texture == null)
            {
                return;
            }

            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        // ---- Audio ------------------------------------------------------------------

        private void ConfigureAudio(VideoClip clip)
        {
            if (clip.audioTrackCount == 0 || videoAudioSource == null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                return;
            }

            if (videoAudioSource.outputAudioMixerGroup == null)
            {
                var manager = AudioManager.Instance;

                if (manager != null && manager.MusicMixerGroup != null)
                {
                    videoAudioSource.outputAudioMixerGroup = manager.MusicMixerGroup;
                    videoAudioSource.volume = 1f;
                }
                else
                {
                    // No mixer to lean on: honour the Music slider by hand.
                    var save = ServiceLocator.Get<ISaveService>();
                    videoAudioSource.volume = save?.Data != null ? Mathf.Clamp01(save.Data.musicVolume) : 1f;
                }
            }

            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
        }

        private void HoldMusic()
        {
            if (_musicHeld)
            {
                return;
            }

            // Resolved lazily: the flow may call Play() from its Start before
            // this component's own Start has run.
            _audio ??= ServiceLocator.Get<IAudioService>();

            if (_audio == null)
            {
                return;
            }

            _audio.StopToolLoop();
            _audio.SetMusicPaused(true);
            _musicHeld = true;
        }

        private void ReleaseMusic()
        {
            if (!_musicHeld)
            {
                return;
            }

            _musicHeld = false;
            _audio?.SetMusicPaused(false);
        }

        // ---- Visibility -------------------------------------------------------------

        private void ShowActive()
        {
            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = true;
            }

            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            if (skipHint != null)
            {
                skipHint.gameObject.SetActive(false);
            }
        }

        private void ShowIdle()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            if (skipHint != null)
            {
                skipHint.gameObject.SetActive(false);
            }

            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = false;
            }
        }

        // ---- Wiring -----------------------------------------------------------------

        private void ResolveReferences()
        {
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
            }

            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
            }

            if (overlayCanvas == null)
            {
                overlayCanvas = GetComponent<Canvas>();
            }

            if (fitter == null && videoSurface != null)
            {
                fitter = videoSurface.GetComponent<AspectRatioFitter>();
            }

            if (videoAudioSource == null)
            {
                videoAudioSource = GetComponent<AudioSource>();

                if (videoAudioSource == null)
                {
                    videoAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            videoAudioSource.playOnAwake = false;
            videoAudioSource.loop = false;
            videoAudioSource.spatialBlend = 0f;

            if (videoPlayer == null)
            {
                Debug.LogError("[CutscenePlayer] No VideoPlayer assigned or found on this object. " +
                               "Every cutscene will be skipped. Add a Video Player component.", this);
            }

            if (videoSurface == null)
            {
                Debug.LogError("[CutscenePlayer] No Video Surface assigned. Drag the 'VideoSurface' " +
                               "RawImage child into the Video Surface field.", this);
            }

            if (group == null)
            {
                Debug.LogWarning("[CutscenePlayer] No CanvasGroup found. The overlay cannot fade or " +
                                 "block taps. Add a Canvas Group to this object.", this);
            }
        }

        private void ConfigureVideoPlayer()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.clip = null;

            videoPlayer.loopPointReached += OnLoopPointReached;
            videoPlayer.errorReceived += OnErrorReceived;
        }

        private string NameOfClip()
        {
            return videoPlayer != null && videoPlayer.clip != null ? videoPlayer.clip.name : "(none)";
        }
    }
}
