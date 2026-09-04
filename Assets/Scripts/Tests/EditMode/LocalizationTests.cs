// ============================================================
// LocalizationTests — verifies the key-to-string layer the whole UI sits on.
// WHAT & WHY: Every player-facing word in the game is a key resolved at runtime.
//   If lookup silently returns null or throws, the failure shows up as blank
//   buttons or a dead screen rather than as an obvious error, so the fallback
//   behaviour is worth pinning down in tests.
// KEY DECISIONS:
//   - The "missing key returns the key itself" rule is tested explicitly. It is
//     the single most important property here: it guarantees a mis-typed key can
//     never blank out a button or crash a screen, and it makes the mistake
//     visible on screen during playtesting.
//   - LocaleTable is exercised directly as well as through the service. It has
//     its own lazy-index and duplicate-key logic that is easier to pin down
//     without a MonoBehaviour in the way.
//   - Private serialized fields are set by reflection. The alternative would be
//     adding test-only public setters to production code, which trades a real
//     API surface for test convenience; reflection keeps the seam in the tests.
//   - Every GameObject is destroyed in TearDown and ServiceLocator is cleared,
//     because LocalizationService registers itself and a leaked instance would
//     make later tests pass or fail depending on run order.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open Window -> General -> Test Runner.
// [ ] Select the "EditMode" tab, then click "Run All".
// ---------------------------------------------------------------

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RestoriumEmporium.Core;
using RestoriumEmporium.Localization;
using UnityEngine;

namespace RestoriumEmporium.Tests
{
    public class LocalizationTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
            ServiceLocator.Clear();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Several paths below log an intentional warning (duplicate key, unknown
        /// locale, malformed format string). Those warnings are the behaviour under
        /// test, so they must not be treated as test failures.
        /// </summary>
        private static void IgnoreExpectedWarnings()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        private LocaleTable MakeTable(string code, params (string key, string value)[] rows)
        {
            var table = ScriptableObject.CreateInstance<LocaleTable>();
            table.localeCode = code;
            table.entries = new List<LocaleTable.Entry>();

            foreach (var (key, value) in rows)
            {
                table.entries.Add(new LocaleTable.Entry { key = key, value = value });
            }

            _spawned.Add(table);
            return table;
        }

        private LocalizationService MakeService(LocaleTable[] tables, string fallback)
        {
            var go = new GameObject("LocalizationService");
            _spawned.Add(go);

            var service = go.AddComponent<LocalizationService>();
            SetPrivate(service, "tables", tables);
            SetPrivate(service, "fallbackLocaleCode", fallback);

            // Awake is not called on an inactive-to-active AddComponent in EditMode,
            // so invoke it explicitly to reach the same state as a real scene load.
            InvokePrivate(service, "Awake");

            return service;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(info, Is.Not.Null,
                $"Field '{field}' no longer exists on {target.GetType().Name}. " +
                "Update this test to match the renamed field.");

            info.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string method)
        {
            var info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);

            info?.Invoke(target, null);
        }

        // ---------------------------------------------------------------
        // LocaleTable
        // ---------------------------------------------------------------

        [Test]
        public void Table_TryGet_FindsAnExistingKey()
        {
            var table = MakeTable("pt-BR", ("ui.journal.restore", "Restaurar"));

            Assert.That(table.TryGet("ui.journal.restore", out var value), Is.True);
            Assert.That(value, Is.EqualTo("Restaurar"));
        }

        [Test]
        public void Table_TryGet_ReportsAMissingKey()
        {
            var table = MakeTable("pt-BR", ("a", "1"));

            Assert.That(table.TryGet("nope", out _), Is.False);
        }

        [Test]
        public void Table_TryGet_HandlesNullAndEmptyKeys()
        {
            var table = MakeTable("pt-BR", ("a", "1"));

            Assert.DoesNotThrow(() =>
            {
                Assert.That(table.TryGet(null, out _), Is.False);
                Assert.That(table.TryGet(string.Empty, out _), Is.False);
            });
        }

        [Test]
        public void Table_DuplicateKeys_ResolveToTheFirstOccurrence()
        {
            var table = MakeTable("pt-BR", ("dup", "primeiro"), ("dup", "segundo"));

            IgnoreExpectedWarnings();

            table.TryGet("dup", out var value);

            Assert.That(value, Is.EqualTo("primeiro"),
                "A stable winner makes the duplicate-key warning actionable.");
        }

        [Test]
        public void Table_Invalidate_PicksUpEditedEntries()
        {
            var table = MakeTable("pt-BR", ("a", "antigo"));

            table.TryGet("a", out _);                       // builds the index
            table.entries[0] = new LocaleTable.Entry { key = "a", value = "novo" };
            table.Invalidate();

            table.TryGet("a", out var value);

            Assert.That(value, Is.EqualTo("novo"));
        }

        // ---------------------------------------------------------------
        // LocalizationService
        // ---------------------------------------------------------------

        [Test]
        public void Service_Get_ResolvesFromTheActiveTable()
        {
            var pt = MakeTable("pt-BR", ("ui.title.newGame", "Novo jogo"));
            var service = MakeService(new[] { pt }, "pt-BR");

            Assert.That(service.Get("ui.title.newGame"), Is.EqualTo("Novo jogo"));
        }

        [Test]
        public void Service_Get_FallsBackToTheKeyWhenMissing()
        {
            var pt = MakeTable("pt-BR", ("a", "1"));
            var service = MakeService(new[] { pt }, "pt-BR");

            Assert.That(service.Get("ui.does.not.exist"), Is.EqualTo("ui.does.not.exist"),
                "Returning the key keeps a typo visible but harmless. Returning " +
                "null or throwing would blank the UI or kill the screen.");
        }

        [Test]
        public void Service_Get_FallsBackToTheFallbackTable()
        {
            var pt = MakeTable("pt-BR", ("only.pt", "so em portugues"));
            var en = MakeTable("en", ("only.en", "english only"));
            var service = MakeService(new[] { en, pt }, "pt-BR");

            service.SetLocale("en");

            Assert.That(service.Get("only.pt"), Is.EqualTo("so em portugues"),
                "A key missing from the active language should fall through to " +
                "the fallback language before giving up.");
        }

        [Test]
        public void Service_SetLocale_IgnoresAnUnknownCode()
        {
            var pt = MakeTable("pt-BR", ("a", "1"));
            var service = MakeService(new[] { pt }, "pt-BR");
            var before = service.CurrentLocaleCode;

            IgnoreExpectedWarnings();
            service.SetLocale("kl-KL");

            Assert.That(service.CurrentLocaleCode, Is.EqualTo(before),
                "Switching to a language that was never shipped must not strand " +
                "the player in an untranslated build.");
        }

        [Test]
        public void Service_Format_SubstitutesArguments()
        {
            var pt = MakeTable("pt-BR", ("progress", "{0} de {1}"));
            var service = MakeService(new[] { pt }, "pt-BR");

            Assert.That(service.Format("progress", 2, 6), Is.EqualTo("2 de 6"));
        }

        [Test]
        public void Service_Format_DoesNotThrowOnAMalformedFormatString()
        {
            var pt = MakeTable("pt-BR", ("bad", "{0} de {7}"));
            var service = MakeService(new[] { pt }, "pt-BR");

            IgnoreExpectedWarnings();

            string result = null;
            Assert.DoesNotThrow(() => result = service.Format("bad", 1));
            Assert.That(result, Is.Not.Null.And.Not.Empty,
                "A bad format string in an asset must degrade to raw text, not " +
                "take down the screen that was showing it.");
        }

        [Test]
        public void Service_TryGet_ReportsWhetherTheKeyReallyExisted()
        {
            var pt = MakeTable("pt-BR", ("real", "existe"));
            var service = MakeService(new[] { pt }, "pt-BR");

            Assert.That(service.TryGet("real", out var found), Is.True);
            Assert.That(found, Is.EqualTo("existe"));
            Assert.That(service.TryGet("fake", out _), Is.False);
        }
    }
}
