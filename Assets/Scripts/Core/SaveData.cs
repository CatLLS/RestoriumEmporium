// ============================================================
// SaveData — the entire durable state of the MVP, as one serialisable POCO.
// WHAT & WHY: §4 of the build spec requires that the app survive being killed
//   at any instant. Keeping every durable field in one flat class means the
//   whole save is one JsonUtility round-trip with no reflection surprises and
//   no partial writes.
// KEY DECISIONS:
//   - Plain public fields, no properties: JsonUtility serialises fields only.
//   - A 'version' field is written from day one. Migrating a save format is
//     cheap when the version is already there and impossible when it is not.
//   - Enums are stored as ints by JsonUtility, which is exactly why GameEnums
//     pins explicit numeric values.
//   - Progress is stored as (posterId, stageIndex) rather than a screen alone,
//     because the screen is derived from the stage — one source of truth for
//     "where was the player".
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. This is a data class; SaveManager owns the only instance.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Core
{
    [Serializable]
    public class SaveData
    {
        /// <summary>Bumped whenever the shape of this class changes.</summary>
        public int version = CurrentVersion;

        public const int CurrentVersion = 1;

        // ---- Settings ----
        public string localeCode = "pt-BR";
        public float musicVolume = 1f;
        public float sfxVolume = 1f;

        // ---- Tutorial ----
        public bool tutorialCompleted;
        public int tutorialStepIndex;

        // ---- Restoration progress ----
        /// <summary>Empty until the player starts a poster from the journal.</summary>
        public string currentPosterId = string.Empty;

        /// <summary>Index into PosterData.stages. -1 means "not started".</summary>
        public int currentStageIndex = -1;

        /// <summary>Set once the whole restoration sequence has been finished.</summary>
        public bool posterCompleted;

        /// <summary>Where to drop the player back on relaunch.</summary>
        public GameScreen lastScreen = GameScreen.Journal;

        /// <summary>True once the player has seen the title screen at least once.</summary>
        public bool hasPlayedBefore;

        public bool IsRestorationInProgress =>
            !string.IsNullOrEmpty(currentPosterId) && currentStageIndex >= 0 && !posterCompleted;
    }
}
