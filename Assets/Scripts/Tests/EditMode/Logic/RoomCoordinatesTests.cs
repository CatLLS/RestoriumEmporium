// ============================================================
// RoomCoordinatesTests — unit tests for the decoration room maths.
// WHAT & WHY: A wrong conversion here would silently move every saved
//   decoration, or let one be dragged off-screen for good. These pin the
//   round-trip, the clamping rule and the bad-input fallbacks.
// KEY DECISIONS:
//   - NUnit only, no UnityEngine, so they run with plain `dotnet test` as well
//     as in Unity's Test Runner.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Window -> General -> Test Runner -> EditMode -> Run All.
// ---------------------------------------------------------------

using NUnit.Framework;
using RestoriumEmporium.Decor;

namespace RestoriumEmporium.Tests.Logic
{
    public class RoomCoordinatesTests
    {
        private const float Eps = 0.0001f;

        // A stretch-anchored, centre-pivot room of 412x917: rect.x/y = -w/2, -h/2.
        private static readonly RoomRect Room = new RoomRect(-206f, -458.5f, 412f, 917f);

        [Test]
        public void ToLocal_CornersAndCentre()
        {
            RoomCoordinates.ToLocal(Room, 0f, 0f, out var x, out var y);
            Assert.AreEqual(-206f, x, Eps);
            Assert.AreEqual(-458.5f, y, Eps);

            RoomCoordinates.ToLocal(Room, 1f, 1f, out x, out y);
            Assert.AreEqual(206f, x, Eps);
            Assert.AreEqual(458.5f, y, Eps);

            RoomCoordinates.ToLocal(Room, 0.5f, 0.5f, out x, out y);
            Assert.AreEqual(0f, x, Eps);
            Assert.AreEqual(0f, y, Eps);
        }

        [Test]
        public void RoundTrip_ReturnsSameNormalized()
        {
            RoomCoordinates.ToLocal(Room, 0.143f, 0.579f, out var x, out var y);
            RoomCoordinates.ToNormalized(Room, x, y, out var nx, out var ny);
            Assert.AreEqual(0.143f, nx, Eps);
            Assert.AreEqual(0.579f, ny, Eps);
        }

        [Test]
        public void ToNormalized_BottomLeftOriginRoom()
        {
            var room = new RoomRect(0f, 0f, 200f, 100f);
            RoomCoordinates.ToNormalized(room, 50f, 25f, out var nx, out var ny);
            Assert.AreEqual(0.25f, nx, Eps);
            Assert.AreEqual(0.25f, ny, Eps);
        }

        [Test]
        public void Clamp_KeepsWholeItemInside()
        {
            // 100 px wide item in a 412 px room: centre can't go below 50/412.
            float nx = -1f, ny = 2f;
            RoomCoordinates.Clamp(Room, 100f, 100f, ref nx, ref ny);
            Assert.AreEqual(50f / 412f, nx, Eps);
            Assert.AreEqual(1f - 50f / 917f, ny, Eps);
        }

        [Test]
        public void Clamp_LeavesInsideValuesAlone()
        {
            float nx = 0.4f, ny = 0.6f;
            RoomCoordinates.Clamp(Room, 50f, 50f, ref nx, ref ny);
            Assert.AreEqual(0.4f, nx, Eps);
            Assert.AreEqual(0.6f, ny, Eps);
        }

        [Test]
        public void Clamp_ItemBiggerThanRoom_IsCentred()
        {
            float nx = 0.1f, ny = 0.9f;
            RoomCoordinates.Clamp(Room, 500f, 1000f, ref nx, ref ny);
            Assert.AreEqual(0.5f, nx, Eps);
            Assert.AreEqual(0.5f, ny, Eps);
        }

        [Test]
        public void Clamp_ZeroSizeItem_ClampsCentreTo01()
        {
            float nx = 1.5f, ny = -0.5f;
            RoomCoordinates.Clamp(Room, 0f, 0f, ref nx, ref ny);
            Assert.AreEqual(1f, nx, Eps);
            Assert.AreEqual(0f, ny, Eps);
        }

        [Test]
        public void NaNAndInfinity_FallBackToCentre()
        {
            float nx = float.NaN, ny = float.PositiveInfinity;
            RoomCoordinates.Clamp(Room, 10f, 10f, ref nx, ref ny);
            Assert.AreEqual(0.5f, nx, Eps);
            Assert.AreEqual(0.5f, ny, Eps);

            RoomCoordinates.ToLocal(Room, float.NaN, float.NaN, out var x, out var y);
            Assert.AreEqual(0f, x, Eps);
            Assert.AreEqual(0f, y, Eps);
        }

        [Test]
        public void EmptyRoom_DoesNotDivideByZero()
        {
            var empty = new RoomRect(0f, 0f, 0f, 0f);
            RoomCoordinates.ToNormalized(empty, 10f, 10f, out var nx, out var ny);
            Assert.AreEqual(0.5f, nx, Eps);
            Assert.AreEqual(0.5f, ny, Eps);
            Assert.IsTrue(empty.IsEmpty);

            float cx = 0.9f, cy = 0.1f;
            RoomCoordinates.Clamp(empty, 10f, 10f, ref cx, ref cy);
            Assert.AreEqual(0.5f, cx, Eps);
            Assert.AreEqual(0.5f, cy, Eps);
        }

        [Test]
        public void LocalToClampedNormalized_DragPastEdge_StopsAtEdge()
        {
            // Finger dragged far to the right of the room.
            RoomCoordinates.LocalToClampedNormalized(Room, 5000f, 0f, 94f, 180f, out var nx, out var ny);
            Assert.AreEqual(1f - 47f / 412f, nx, Eps);
            Assert.AreEqual(0.5f, ny, Eps);
        }

        [Test]
        public void NegativeSizes_AreTreatedAsZero()
        {
            var room = new RoomRect(0f, 0f, -10f, 100f);
            Assert.IsTrue(room.IsEmpty);
            float nx = 0.2f, ny = 0.3f;
            RoomCoordinates.Clamp(new RoomRect(0f, 0f, 100f, 100f), -20f, -20f, ref nx, ref ny);
            Assert.AreEqual(0.2f, nx, Eps);
            Assert.AreEqual(0.3f, ny, Eps);
        }
    }
}
