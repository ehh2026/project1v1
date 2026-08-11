using System.Windows;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests
{
    /// <summary>
    /// Tests for ManualLayoutOverrideStore - in-memory shaft/head pair overrides and composite pin endpoints.
    /// </summary>
    public class ManualLayoutOverrideStoreTests
    {
        [Fact]
        public void InitialState_HasNoPendingOverrides()
        {
            var store = new ManualLayoutOverrideStore();
            Assert.False(store.HasPendingOverrides);
        }

        [Fact]
        public void SetOverride_StoresOverride()
        {
            var store = new ManualLayoutOverrideStore();
            store.SetOverride("Location1", "pair1", "head1.png");

            Assert.True(store.HasPendingOverrides);
            Assert.True(store.TryGetOverride("Location1", out var pairId, out var headPath));
            Assert.Equal("pair1", pairId);
            Assert.Equal("head1.png", headPath);
        }

        [Fact]
        public void TryGetOverride_ReturnsStoredOverride()
        {
            var store = new ManualLayoutOverrideStore();
            store.SetOverride("Location1", "pair1", "head1.png");

            var success = store.TryGetOverride("Location1", out var pairId, out var headPath);
            Assert.True(success);
            Assert.Equal("pair1", pairId);
            Assert.Equal("head1.png", headPath);
        }

        [Fact]
        public void TryGetOverride_WhenNotSet_ReturnsFalse()
        {
            var store = new ManualLayoutOverrideStore();
            var success = store.TryGetOverride("NonExistent", out var pairId, out var headPath);

            Assert.False(success);
            Assert.Null(pairId);
            Assert.Null(headPath);
        }

        [Fact]
        public void GetAllOverrides_ReturnsAllStored()
        {
            var store = new ManualLayoutOverrideStore();
            store.SetOverride("Location1", "pair1", "head1.png");
            store.SetOverride("Location2", "pair2", "head2.png");

            var overrides = store.GetAllOverrides();
            Assert.Equal(2, overrides.Count);
            Assert.True(overrides.ContainsKey("Location1"));
            Assert.True(overrides.ContainsKey("Location2"));
        }

        [Fact]
        public void ClearOverrides_RemovesOverridesButNotEndpoints()
        {
            var store = new ManualLayoutOverrideStore();
            store.SetOverride("Location1", "pair1", "head1.png");
            store.RecordEndpoints("Location1", new Point(10, 10), new Point(20, 20));

            store.ClearOverrides();

            Assert.False(store.HasPendingOverrides);
            Assert.True(store.TryGetEndpoints("Location1", out var original, out var extended));
            Assert.Equal(new Point(10, 10), original);
            Assert.Equal(new Point(20, 20), extended);
        }

        [Fact]
        public void RecordEndpoints_StoresEndpoints()
        {
            var store = new ManualLayoutOverrideStore();
            store.RecordEndpoints("Location1", new Point(10, 10), new Point(20, 20));

            var success = store.TryGetEndpoints("Location1", out var original, out var extended);
            Assert.True(success);
            Assert.Equal(new Point(10, 10), original);
            Assert.Equal(new Point(20, 20), extended);
        }

        [Fact]
        public void TryGetEndpoints_ReturnsStoredEndpoints()
        {
            var store = new ManualLayoutOverrideStore();
            store.RecordEndpoints("Location1", new Point(10, 10), new Point(20, 20));

            var success = store.TryGetEndpoints("Location1", out var original, out var extended);
            Assert.True(success);
            Assert.Equal(new Point(10, 10), original);
            Assert.Equal(new Point(20, 20), extended);
        }

        [Fact]
        public void TryGetEndpoints_WhenNotSet_ReturnsFalse()
        {
            var store = new ManualLayoutOverrideStore();
            var success = store.TryGetEndpoints("NonExistent", out var original, out var extended);

            Assert.False(success);
            Assert.Equal(default, original);
            Assert.Equal(default, extended);
        }

        [Fact]
        public void ClearAll_RemovesOverridesAndEndpoints()
        {
            var store = new ManualLayoutOverrideStore();
            store.SetOverride("Location1", "pair1", "head1.png");
            store.RecordEndpoints("Location1", new Point(10, 10), new Point(20, 20));

            store.ClearAll();

            Assert.False(store.HasPendingOverrides);
            Assert.False(store.TryGetEndpoints("Location1", out var original, out var extended));
        }

        [Fact]
        public void SetOverride_OverwritesPrevious()
        {
            var store = new ManualLayoutOverrideStore();
            store.SetOverride("Location1", "pair1", "head1.png");
            store.SetOverride("Location1", "pair2", "head2.png");

            var success = store.TryGetOverride("Location1", out var pairId, out var headPath);
            Assert.True(success);
            Assert.Equal("pair2", pairId);
            Assert.Equal("head2.png", headPath);
        }

        [Fact]
        public void HasPendingOverrides_ReflectsState()
        {
            var store = new ManualLayoutOverrideStore();
            Assert.False(store.HasPendingOverrides);

            store.SetOverride("Location1", "pair1", "head1.png");
            Assert.True(store.HasPendingOverrides);

            store.ClearOverrides();
            Assert.False(store.HasPendingOverrides);
        }
    }
}
