// ============================================================
// PosterCatalog — every poster, in journal / unlock order.
// WHAT & WHY: The journal shows one page per poster, and posters unlock in
//   order (poster 2 after poster 1). One ordered list is the single source of
//   both facts; nothing else in the game hardcodes a poster.
// KEY DECISIONS:
//   - Filled by the EDITOR ("Restorium > Rebuild Catalogs & Locale Tables" and an
//     asset postprocessor): every PosterData under Assets/Data/ sorted by
//     PosterData.journalOrder. Adding a poster = run "Restorium > Posters >
//     Create/Update Poster Data" for its art folder; no code, no scene edits.
//   - OrderedIds is cached because IPosterProgress.IsUnlocked takes an id list
//     and the journal asks on every page turn.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand. Lives at Assets/Data/Catalogs/PosterCatalog.asset, created and
//     refreshed automatically. GameFlowController and JournalScreen have a "Posters"
//     field for it (the scene builder assigns it).
// ---------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Data
{
    [CreateAssetMenu(menuName = "Restorium/Poster Catalog", fileName = "PosterCatalog")]
    public class PosterCatalog : ScriptableObject
    {
        [Tooltip("Filled automatically by the editor. Order = journal page order = unlock order.")]
        public List<PosterData> posters = new List<PosterData>();

        private List<string> _orderedIds;

        public int Count => posters != null ? posters.Count : 0;

        public PosterData Get(int index) =>
            posters != null && index >= 0 && index < posters.Count ? posters[index] : null;

        public PosterData Find(string posterId)
        {
            if (string.IsNullOrEmpty(posterId) || posters == null)
            {
                return null;
            }

            foreach (var poster in posters)
            {
                if (poster != null && poster.posterId == posterId)
                {
                    return poster;
                }
            }

            return null;
        }

        public int IndexOf(string posterId)
        {
            for (var i = 0; i < Count; i++)
            {
                if (posters[i] != null && posters[i].posterId == posterId)
                {
                    return i;
                }
            }

            return -1;
        }

        public IReadOnlyList<string> OrderedIds
        {
            get
            {
                if (_orderedIds == null)
                {
                    _orderedIds = new List<string>(Count);

                    for (var i = 0; i < Count; i++)
                    {
                        _orderedIds.Add(posters[i] != null ? posters[i].posterId : string.Empty);
                    }
                }

                return _orderedIds;
            }
        }

        public void Invalidate() => _orderedIds = null;

        private void OnValidate() => Invalidate();
    }
}
