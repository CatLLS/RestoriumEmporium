// ============================================================
// AudioManager — the single audio service: one-shots, a tool loop, and music.
// WHAT & WHY: Grew out of the title screen MenuAudioManager, which could only
//   play a button click. The restoration tools need a sound that starts on
//   finger-down and stops on finger-up, so this adds a dedicated looping channel
//   and an SfxLibrary lookup, and registers itself as IAudioService so no caller
//   ever holds a reference to this concrete class.
// KEY DECISIONS:
//   - The original serialised field names (mainMixer, sfxSource, musicSource,
//     buttonClickSound) are kept EXACTLY. SampleScene already has values in
//     them; renaming a field would silently blank it on the next scene load.
//     The file also kept its original .meta GUID for the same reason.
//   - One looping channel, not one per tool. Only one tool can be dragging at a
//     time; a second channel could only ever produce two overlapping tool
//     sounds, which is a bug rather than a feature.
//   - StartToolLoop with the clip already playing is a no-op instead of a
//     restart. It is called from a drag handler that may fire repeatedly, and
//     restarting would machine-gun the attack of the sound.
//   - PlaySfx(ButtonClick) falls back to the legacy buttonClickSound field when
//     the library has no entry, so the title screen keeps working before the
//     SfxLibrary asset exists.
//   - Registers into ServiceLocator in Awake and unregisters in OnDestroy with
//     an identity check, so a duplicate arriving on a scene reload cannot
//     unregister the surviving instance on its way out.
//   - Volume conversion is untouched from the original: 0 means -80 dB, and
//     everything else is 20*log10(linear). The mixer exposes MusicVolume and
//     SFXVolume; those exact parameter names must stay in the mixer asset.
//   - Batch 2: SetMusicPaused(bool) PAUSES (not stops) the music while a
//     cutscene plays, so the track picks up where it was instead of restarting.
//     A PlayMusic call while paused loads the new track but keeps it paused.
//     MusicMixerGroup exposes the music source's mixer group so video
//     soundtracks can be routed through the same Music slider.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// SampleScene already has this component. Do NOT delete and re-add it —
// its Audio Mixer reference is already assigned. Follow these steps instead.
//
// A) FINISH THE EXISTING OBJECT IN SampleScene.unity
// [x] File -> Open Scene -> Assets/Scenes/SampleScene.unity.
// [x] In the Hierarchy find the object that carries this script (type "Audio"
//     into the Hierarchy search box). Rename it to exactly: Systems
// [x] With Systems selected, in the Inspector confirm Main Mixer still points at
//     Assets/Audio/MainMixer. If it went blank, drag MainMixer back into it.
// [x] Right-click Systems -> Create Empty. Name the child exactly: SfxSource
//       Add Component -> Audio -> Audio Source.
//       Untick Play On Awake. Untick Loop.
//       Set Output to the SFX group of MainMixer.
// [x] Right-click Systems -> Create Empty. Name the child exactly: MusicSource
//       Add Component -> Audio -> Audio Source.
//       Untick Play On Awake. TICK Loop.
//       Set Output to the Music group of MainMixer.
// [x] Right-click Systems -> Create Empty. Name the child exactly: ToolLoopSource
//       Add Component -> Audio -> Audio Source.
//       Untick Play On Awake. TICK Loop.
//       Set Output to the SFX group of MainMixer.
// [x] Select Systems again and drag the three children into the matching
//     Inspector fields: Sfx Source, Music Source, Tool Loop Source.
// [x] Drag Assets/Audio/Data/SfxLibrary into the Sfx Library field.
// [x] Optional: drag Assets/Audio/(bgSongThatPlaysDuringTheGamePlay)lilliben-
//     dark-ambient-background-mystery-365195 into Startup Music if you want
//     music from the title screen onward. Leave it empty for no boot music.
// [x] Save the scene (Ctrl+S).
//
// B) MAKE THE MIXER EXPOSE THE TWO VOLUME PARAMETERS (do this once)
// [x] Double-click Assets/Audio/MainMixer to open the Audio Mixer window.
// [x] Confirm there are groups named Music and SFX under Master. If not,
//     right-click Master -> Add child group and name them exactly that.
// [x] Click the Music group. In the Inspector, right-click the Volume label and
//     choose the Expose ... to script entry.
// [x] Click the SFX group and do the same for its Volume.
// [x] Back in the Audio Mixer window click Exposed Parameters (top right) and
//     rename the two entries to exactly:  MusicVolume  and  SFXVolume
//     They are case-sensitive; this script looks them up by those strings.
//
// C) CARRY IT INTO THE OTHER SCENES
// [ ] This object survives scene loads by itself (DontDestroyOnLoad), so do NOT
//     add a second copy to Game.unity or ThanksForPlaying.unity. A duplicate
//     destroys itself in Awake, but you would lose its Inspector values.
// [x] File -> Build Profiles -> Scene List: SampleScene must be index 0, so the
//     game always boots through the scene that owns this object.
// ---------------------------------------------------------------

using UnityEngine;
using UnityEngine.Audio; // Required to control the Audio Mixer

namespace RestoriumEmporium.Audio
{
    using RestoriumEmporium.Core;

    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour, IAudioService
    {
        /// <summary>Mixer parameter names. Must match the exposed names in MainMixer.</summary>
        private const string MusicVolumeParam = "MusicVolume";
        private const string SfxVolumeParam = "SFXVolume";

        private const float SilenceDb = -80f;

        // This static reference lets ANY script in your game find the AudioManager instantly
        public static AudioManager Instance { get; private set; }

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer mainMixer;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;

        [Tooltip("Dedicated looping channel for the tool currently being dragged.")]
        [SerializeField] private AudioSource toolLoopSource;

        [Header("Audio Clips")]
        [Tooltip("Legacy title-screen click. Only used when the SfxLibrary has no " +
                 "ButtonClick entry yet.")]
        [SerializeField] private AudioClip buttonClickSound;

        [Tooltip("Played on Awake. Leave empty for no music at boot.")]
        [SerializeField] private AudioClip startupMusic;

        [Header("Library")]
        [Tooltip("Maps every SfxId to a clip. A missing clip is silence, not an error.")]
        [SerializeField] private SfxLibrary sfxLibrary;

        private SfxId _toolLoopId = SfxId.None;
        private bool _musicPaused;

        private void Awake()
        {
            // Singleton pattern: Ensures only ONE AudioManager ever exists
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // Keeps this alive across scenes
            }
            else
            {
                Destroy(gameObject); // Destroys duplicates if returning to title screen
                return;
            }

            ServiceLocator.Register<IAudioService>(this);

            if (startupMusic != null)
            {
                PlayMusic(startupMusic);
            }
        }

        private void OnDestroy()
        {
            // The identity check matters: a duplicate destroyed in Awake must not
            // unregister the instance that survived.
            if (!ReferenceEquals(Instance, this))
            {
                return;
            }

            ServiceLocator.Unregister<IAudioService>(this);
            Instance = null;
        }

        // ---- IAudioService -------------------------------------------------

        public void PlaySfx(SfxId id)
        {
            if (sfxSource == null || id == SfxId.None)
            {
                return;
            }

            if (sfxLibrary != null &&
                sfxLibrary.TryResolve(id, out AudioClip clip, out float volume, out float pitch))
            {
                sfxSource.pitch = pitch;
                sfxSource.PlayOneShot(clip, volume);
                return;
            }

            // Fallback so the title screen still clicks before SfxLibrary exists.
            if (id == SfxId.ButtonClick && buttonClickSound != null)
            {
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(buttonClickSound);
            }

            // Anything else is deliberate silence: the sound is simply not authored yet.
        }

        public void StartToolLoop(SfxId id)
        {
            if (toolLoopSource == null)
            {
                return;
            }

            if (id == SfxId.None)
            {
                StopToolLoop();
                return;
            }

            // Already running this exact sound: leave it alone rather than
            // restarting its attack on every drag callback.
            if (_toolLoopId == id && toolLoopSource.isPlaying)
            {
                return;
            }

            if (sfxLibrary == null ||
                !sfxLibrary.TryResolve(id, out AudioClip clip, out float volume, out float pitch))
            {
                StopToolLoop();
                return;
            }

            _toolLoopId = id;
            toolLoopSource.clip = clip;
            toolLoopSource.volume = volume;
            toolLoopSource.pitch = pitch;
            toolLoopSource.loop = true;
            toolLoopSource.Play();
        }

        public void StopToolLoop()
        {
            _toolLoopId = SfxId.None;

            if (toolLoopSource == null)
            {
                return;
            }

            toolLoopSource.Stop();
            toolLoopSource.clip = null;
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (musicSource == null || clip == null)
            {
                return;
            }

            if (musicSource.clip == clip && musicSource.loop == loop &&
                (musicSource.isPlaying || (_musicPaused && musicSource.time > 0f)))
            {
                return;
            }

            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.Play();

            if (_musicPaused)
            {
                // A cutscene is holding the music. Start the new track in the
                // paused state so it resumes from its beginning when released.
                musicSource.Pause();
            }
        }

        public void StopMusic()
        {
            if (musicSource == null)
            {
                return;
            }

            musicSource.Stop();
        }

        /// <inheritdoc />
        public void SetMusicPaused(bool paused)
        {
            if (_musicPaused == paused)
            {
                return;
            }

            _musicPaused = paused;

            if (musicSource == null)
            {
                return;
            }

            if (paused)
            {
                musicSource.Pause();
            }
            else
            {
                // UnPause only resumes a source that was paused mid-play; a
                // stopped source stays stopped, which is the right outcome.
                musicSource.UnPause();
            }
        }

        /// <summary>
        /// The mixer group the music source plays through (MainMixer/Music), or null.
        /// CutscenePlayer routes video soundtracks here when its own AudioSource
        /// has no group assigned, so the Music slider controls them too.
        /// </summary>
        public AudioMixerGroup MusicMixerGroup =>
            musicSource != null ? musicSource.outputAudioMixerGroup : null;

        // Call these functions from your Settings Page Sliders
        public void SetMusicVolume(float volume)
        {
            SetMixerVolume(MusicVolumeParam, volume);
        }

        /// <summary>0..1 linear. Named to match IAudioService.</summary>
        public void SetSfxVolume(float volume)
        {
            SetMixerVolume(SfxVolumeParam, volume);
        }

        /// <summary>
        /// Legacy name, kept because a settings slider may already reference it by
        /// string in a UnityEvent. Forwards to SetSfxVolume.
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            SetSfxVolume(volume);
        }

        /// <summary>Legacy entry point used by the title screen button UnityEvent.</summary>
        public void PlayButtonClick()
        {
            PlaySfx(SfxId.ButtonClick);
        }

        private void SetMixerVolume(string parameterName, float volume)
        {
            if (mainMixer == null)
            {
                return;
            }

            volume = Mathf.Clamp01(volume);

            // Mixer volumes use logarithmic math (-80dB is silent, 0dB is full volume)
            float dB = volume <= 0f ? SilenceDb : Mathf.Log10(volume) * 20f;
            mainMixer.SetFloat(parameterName, dB);
        }
    }
}
