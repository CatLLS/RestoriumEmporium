// ============================================================
// SceneValidationTests — checks the scene builder's static spec tables.
// WHAT & WHY: Runs without the Game scene open (or even loaded) — it only
//   checks that every sprite path SceneBuilderManifest.SpritePaths lists
//   actually resolves through AssetDatabase, and every LocalizedText key
//   SceneBuilderManifest.LocalizedKeys lists actually exists in LocaleSource
//   (both pt-BR and en, since LocaleSource.All() yields one entry per key with
//   both languages already filled in — see LocaleSource.cs). This is the cheap,
//   always-available check: a typo here fails a normal EditMode test run, long
//   before anyone opens Unity and clicks Build Batch 2 Scene Objects.
// KEY DECISIONS:
//   - Editor-only (AssetDatabase, LocaleSource are both Editor-assembly types),
//     so this file needs UnityEditor.TestRunner + a reference to
//     RestoriumEmporium.Editor, which is why RestoriumEmporium.EditTests.asmdef
//     was updated to add that reference (see its own file).
//   - Deliberately does NOT open or touch any scene: SceneBuilderManifest is a
//     hand-maintained mirror of the literals inside the Build*.cs files (see
//     that file's own comment for why), so this test's job is only "does the
//     mirror point at real things", not "does the builder actually work" —
//     that needs a human running it once in Unity (see
//     Docs/Batch2/handoff/SCENE_BUILDER.md).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Window -> General -> Test Runner -> EditMode -> Run All, or
//     `dotnet test Tools/logictests` will NOT run this file (it needs
//     UnityEditor types) — use Unity's own Test Runner, or
//     `sh Tools/compilecheck/check.sh` to at least confirm it compiles.
// ---------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace RestoriumEmporium.Tests
{
    using RestoriumEmporium.EditorTools;

    [TestFixture]
    public class SceneValidationTests
    {
        [Test]
        public void EverySpritePathInTheManifestExists()
        {
            var missing = new List<string>();

            foreach (var path in SceneBuilderManifest.SpritePaths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);

                if (sprite == null)
                {
                    missing.Add(path);
                }
            }

            Assert.IsEmpty(missing,
                "SceneBuilderManifest.SpritePaths references path(s) AssetDatabase cannot load as a Sprite " +
                "(missing file, or not imported as Sprite (2D and UI) — run " +
                "Restorium/Fix Art Import Settings (Sprites)):\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void ManifestHasNoDuplicateSpritePaths()
        {
            var duplicates = SceneBuilderManifest.SpritePaths
                .GroupBy(p => p)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.IsEmpty(duplicates, "Duplicate sprite path(s) in SceneBuilderManifest: " +
                                        string.Join(", ", duplicates));
        }

        [Test]
        public void EveryLocalizedKeyInTheManifestExistsInLocaleSource()
        {
            var known = new HashSet<string>(LocaleSource.All().Select(e => e.Key));
            var missing = SceneBuilderManifest.LocalizedKeys.Where(k => !known.Contains(k)).ToList();

            Assert.IsEmpty(missing,
                "SceneBuilderManifest.LocalizedKeys references key(s) not defined in any LocaleSource.*.cs " +
                "file (both pt-BR and en come from the same LocaleEntry, so a missing key here means it is " +
                "missing in BOTH languages):\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void ManifestHasNoDuplicateLocalizedKeys()
        {
            var duplicates = SceneBuilderManifest.LocalizedKeys
                .GroupBy(k => k)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.IsEmpty(duplicates, "Duplicate localized key(s) in SceneBuilderManifest: " +
                                        string.Join(", ", duplicates));
        }

        [Test]
        public void EveryManifestLocalizedKeyHasNonEmptyStringsInBothLanguages()
        {
            var byKey = LocaleSource.All().ToDictionary(e => e.Key, e => e);
            var bad = new List<string>();

            foreach (var key in SceneBuilderManifest.LocalizedKeys)
            {
                if (!byKey.TryGetValue(key, out var entry))
                {
                    continue; // already reported by EveryLocalizedKeyInTheManifestExistsInLocaleSource
                }

                if (string.IsNullOrEmpty(entry.PtBR) || string.IsNullOrEmpty(entry.En))
                {
                    bad.Add(key);
                }
            }

            Assert.IsEmpty(bad, "Key(s) with an empty pt-BR or en string: " + string.Join(", ", bad));
        }
    }
}
