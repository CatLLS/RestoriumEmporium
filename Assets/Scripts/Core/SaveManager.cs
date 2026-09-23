// ============================================================
// SaveManager — the only thing in the game that touches the disk.
// WHAT & WHY: This is a mobile game; Android can kill the process between any
//   two frames, with no warning and no chance to run cleanup code. Implements
//   ISaveService so every system mutates one SaveData object and asks for a
//   write, while all of the failure handling lives here in one place.
// KEY DECISIONS:
//   - Writes are atomic: temp file, then File.Replace. A plain WriteAllText
//     truncates the real file first, so a kill inside that window leaves a
//     zero-byte save and a player who lost everything. Temp-then-swap means the
//     real file is only ever the old complete save or the new complete save.
//   - SaveSoon() sets a flag that LateUpdate consumes, so the drag loop can
//     call it on every pointer move and still cause at most one write per frame.
//     Save() stays synchronous for the few moments (stage completion, app
//     backgrounding) that must not be deferred.
//   - Every read path is defensive: missing file, unparseable JSON, JSON that
//     parses to null, and a version from a future build all resolve to a fresh
//     SaveData plus a warning. Losing a save is bad; refusing to launch is
//     worse, and an exception in Awake would take the whole boot sequence down.
//   - A newer 'version' resets rather than loads. JsonUtility silently drops
//     fields it does not recognise, so a v2 save read by a v1 build would look
//     valid while quietly discarding progress on the next write. Starting clean
//     is honest; corrupting in place is not.
//   - Older versions are migrated in place by SaveMigration right after the
//     parse (plain C#, unit-tested), then EnsureCollections() replaces any null
//     list. A migrated save is marked dirty so the upgraded file is written on
//     the next frame through the same atomic path as every other write.
//   - Data is lazily loaded by its getter as well as by Awake, because Unity
//     gives no ordering guarantee between this Awake and GameBootstrap's.
//   - ResetProgress keeps locale and volumes. Those are device preferences, not
//     progress; flipping a player's language back because they restarted the
//     poster would read as a bug.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Open the Title scene. If the persistent systems object does not exist
//     yet, use the menu GameObject -> Create Empty, then rename the new object
//     to exactly "Systems" (select it in the Hierarchy, press F2, type it).
// [x] Select "Systems" in the Hierarchy. In the Inspector click
//     "Add Component", type "SaveManager", press Enter.
// [x] Leave "File Name" as "save.json". Changing it after release makes every
//     existing player look like a brand new one.
// [x] Leave "Pretty Print" and "Log Writes" unticked for a release build. Tick
//     them while testing if you want a readable save file and write logging.
// [x] Drag the "Systems" object from the Hierarchy into the Project window
//     folder Assets/Prefabs/ to turn it into a prefab. Create that folder first
//     if it is missing: right-click in Project -> Create -> Folder.
// [ ] To test a first launch, delete the save file. On Windows it is at
//     %USERPROFILE%\AppData\LocalLow\<Company Name>\<Product Name>\save.json
//     (those two names come from Edit -> Project Settings -> Player).
// ---------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RestoriumEmporium.Core
{
    [DisallowMultipleComponent]
    public class SaveManager : MonoBehaviour, ISaveService
    {
        private const string TempSuffix = ".tmp";
        private const string DefaultFileName = "save.json";

        [Header("File")]
        [Tooltip("File name inside Application.persistentDataPath. " +
                 "Changing this after release orphans every existing save.")]
        [SerializeField] private string fileName = DefaultFileName;

        [Tooltip("Writes indented JSON. Handy while testing, slightly larger on disk.")]
        [SerializeField] private bool prettyPrint;

        [Header("Diagnostics")]
        [Tooltip("Logs every disk write. Leave off outside of debugging.")]
        [SerializeField] private bool logWrites;

        private SaveData _data;
        private string _savePath;
        private string _tempPath;
        private bool _dirty;

        /// <inheritdoc />
        public event Action Reloaded;

        /// <inheritdoc />
        public SaveData Data
        {
            get
            {
                if (_data == null)
                {
                    LoadFromDisk();
                }

                return _data;
            }
        }

        /// <summary>Absolute path of the save file. Useful in bug reports.</summary>
        public string SavePath
        {
            get
            {
                EnsurePaths();
                return _savePath;
            }
        }

        private void Awake()
        {
            EnsurePaths();

            if (_data == null)
            {
                LoadFromDisk();
            }
        }

        private void LateUpdate()
        {
            // One boolean test per frame; the write only happens on the frames
            // where something actually asked for one.
            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            WriteToDisk();
        }

        /// <inheritdoc />
        public void Save()
        {
            _dirty = false;
            WriteToDisk();
        }

        /// <inheritdoc />
        public void SaveSoon()
        {
            _dirty = true;
        }

        /// <inheritdoc />
        public void Load()
        {
            LoadFromDisk();
            Reloaded?.Invoke();
        }

        /// <inheritdoc />
        public void ResetProgress()
        {
            var previous = Data;

            // Device preferences survive a progress wipe on purpose.
            _data = new SaveData
            {
                localeCode = previous.localeCode,
                musicVolume = previous.musicVolume,
                sfxVolume = previous.sfxVolume,
                hasPlayedBefore = previous.hasPlayedBefore
            };

            Save();
            Reloaded?.Invoke();
        }

        // ---- Application lifecycle -------------------------------------------------
        // Android can kill a backgrounded process without ever calling
        // OnApplicationQuit, so pause and focus loss are the reliable hooks and
        // quit only really covers the desktop and Editor cases.

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Flush();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                Flush();
            }
        }

        private void OnApplicationQuit()
        {
            Flush();
        }

        /// <summary>Writes now if anything is pending. Free when nothing changed.</summary>
        private void Flush()
        {
            if (_dirty)
            {
                Save();
            }
        }

        // ---- Disk ------------------------------------------------------------------

        private void EnsurePaths()
        {
            if (!string.IsNullOrEmpty(_savePath))
            {
                return;
            }

            var safeName = string.IsNullOrEmpty(fileName) ? DefaultFileName : fileName;
            _savePath = Path.Combine(Application.persistentDataPath, safeName);
            _tempPath = _savePath + TempSuffix;
        }

        private void LoadFromDisk()
        {
            EnsurePaths();

            string json = null;

            try
            {
                if (File.Exists(_savePath))
                {
                    json = File.ReadAllText(_savePath, Encoding.UTF8);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Could not read '{_savePath}': {e.Message}. " +
                                 "Starting from a fresh save.", this);
                json = null;
            }

            _data = Parse(json) ?? new SaveData();
        }

        /// <summary>Returns null whenever the text cannot be trusted as a save.</summary>
        private SaveData Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            SaveData parsed;

            try
            {
                parsed = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Save file is corrupt ({e.Message}). " +
                                 "Starting from a fresh save.", this);
                return null;
            }

            if (parsed == null)
            {
                Debug.LogWarning("[SaveManager] Save file parsed to nothing. " +
                                 "Starting from a fresh save.", this);
                return null;
            }

            if (parsed.version > SaveData.CurrentVersion)
            {
                Debug.LogWarning($"[SaveManager] Save version {parsed.version} is newer than this " +
                                 $"build understands ({SaveData.CurrentVersion}). Starting from a " +
                                 "fresh save rather than silently dropping fields.", this);
                return null;
            }

            // Older versions are upgraded in place (v1 -> v2: per-poster progress,
            // tutorial sequences, the MVP poster's unpaid reward). The migrated
            // file is written on the next LateUpdate rather than here, because a
            // load can happen from inside the Data getter at any point in a frame.
            var fromVersion = parsed.version;

            if (SaveMigration.MigrateInPlace(parsed, message => Debug.LogWarning(message, this)))
            {
                Debug.Log($"[SaveManager] Migrated save from version {fromVersion} to " +
                          $"{SaveData.CurrentVersion}.", this);
                _dirty = true;
            }

            // JsonUtility leaves a list null when the JSON omits it (an old or
            // hand-edited file); every reader assumes they exist.
            parsed.EnsureCollections();

            // Stamping the version keeps the file honest about which build last
            // wrote it.
            parsed.version = SaveData.CurrentVersion;
            return parsed;
        }

        private void WriteToDisk()
        {
            EnsurePaths();

            string json;

            try
            {
                json = JsonUtility.ToJson(Data, prettyPrint);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Could not serialise the save: {e.Message}", this);
                return;
            }

            try
            {
                WriteAtomic(json);

                if (logWrites)
                {
                    Debug.Log($"[SaveManager] Wrote {json.Length} chars to '{_savePath}'.", this);
                }
            }
            catch (Exception e)
            {
                // A failed write must never take the game down: the player keeps
                // playing on the in-memory state and the next write may succeed.
                Debug.LogError($"[SaveManager] Could not write '{_savePath}': {e.Message}", this);
                TryDeleteTemp();
            }
        }

        private void WriteAtomic(string json)
        {
            var directory = Path.GetDirectoryName(_savePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // No BOM: JsonUtility.FromJson chokes on one, and a save that only
            // fails on the second launch is a miserable bug to track down.
            var encoding = new UTF8Encoding(false);

            using (var stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, encoding))
            {
                writer.Write(json);
                writer.Flush();

                try
                {
                    // Push the bytes past the OS cache before the swap, so a
                    // power cut cannot leave an empty file holding the real name.
                    stream.Flush(true);
                }
                catch (Exception)
                {
                    // Not every platform honours a hard flush. The swap below is
                    // still the important half of the guarantee.
                }
            }

            if (!File.Exists(_savePath))
            {
                File.Move(_tempPath, _savePath);
                return;
            }

            try
            {
                File.Replace(_tempPath, _savePath, null);
            }
            catch (Exception)
            {
                // Some Android storage backends do not implement Replace. Fall
                // back to delete-then-move: a much narrower window than a
                // truncating write, and the only option left.
                File.Delete(_savePath);
                File.Move(_tempPath, _savePath);
            }
        }

        private void TryDeleteTemp()
        {
            try
            {
                if (File.Exists(_tempPath))
                {
                    File.Delete(_tempPath);
                }
            }
            catch (Exception)
            {
                // Nothing useful to do here; a stale .tmp is harmless and is
                // overwritten by the next write.
            }
        }
    }
}
