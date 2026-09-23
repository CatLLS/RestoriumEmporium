// ============================================================
// LocalePickerTests — first-launch language guess and the settings cycler.
// WHAT & WHY: A wrong pick strands a player in a language they cannot read, so
//   the device-language guess, the saved-choice override and the cycling order
//   are pinned here with plain NUnit (no UnityEngine).
// KEY DECISIONS:
//   - Device language is passed as the SystemLanguage enum NAME, exactly as
//     LocalizationService does, so these tests cover the real call.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Window -> General -> Test Runner -> EditMode -> Run All.
// ---------------------------------------------------------------

using NUnit.Framework;
using RestoriumEmporium.Localization;

namespace RestoriumEmporium.Tests.Logic
{
    public class LocalePickerTests
    {
        private static readonly string[] Both = { "pt-BR", "en" };

        [Test]
        public void NoSavedChoice_PortuguesePhone_GetsPtBR()
        {
            Assert.That(LocalePicker.Pick("", "Portuguese", Both, "pt-BR"), Is.EqualTo("pt-BR"));
        }

        [Test]
        public void NoSavedChoice_AnyOtherPhone_GetsEnglish()
        {
            Assert.That(LocalePicker.Pick("", "German", Both, "pt-BR"), Is.EqualTo("en"));
            Assert.That(LocalePicker.Pick(null, "English", Both, "pt-BR"), Is.EqualTo("en"));
        }

        [Test]
        public void SavedChoice_WinsOverTheDevice()
        {
            Assert.That(LocalePicker.Pick("en", "Portuguese", Both, "pt-BR"), Is.EqualTo("en"));
        }

        [Test]
        public void SavedChoice_IsCaseInsensitive_ButReturnsTableSpelling()
        {
            Assert.That(LocalePicker.Pick("PT-br", "English", Both, "en"), Is.EqualTo("pt-BR"));
        }

        [Test]
        public void UnshippedSavedCode_FallsBackToDeviceGuess()
        {
            Assert.That(LocalePicker.Pick("fr", "Portuguese", Both, "en"), Is.EqualTo("pt-BR"));
        }

        [Test]
        public void GuessNotShipped_UsesFallback()
        {
            Assert.That(LocalePicker.Pick("", "English", new[] { "pt-BR" }, "pt-BR"), Is.EqualTo("pt-BR"));
        }

        [Test]
        public void NothingShipped_ReturnsNull()
        {
            Assert.That(LocalePicker.Pick("en", "English", new string[0], "en"), Is.Null);
        }

        [Test]
        public void Next_CyclesAndWraps()
        {
            Assert.That(LocalePicker.Next("pt-BR", Both), Is.EqualTo("en"));
            Assert.That(LocalePicker.Next("en", Both), Is.EqualTo("pt-BR"));
            Assert.That(LocalePicker.Next("en", Both, -1), Is.EqualTo("pt-BR"));
            Assert.That(LocalePicker.Next("xx", Both), Is.EqualTo("pt-BR"));
        }
    }
}
