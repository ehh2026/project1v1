using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests
{
    /// <summary>
    /// Tests for MapNavigationService - stack-based navigation state management.
    /// </summary>
    public class MapNavigationServiceTests
    {
        [Fact]
        public void InitialState_CanGoBackIsFalse()
        {
            var service = new MapNavigationService();
            Assert.False(service.CanGoBack);
        }

        [Fact]
        public void PushState_IncreasesStackDepth()
        {
            var service = new MapNavigationService();
            var state = new ZoomState
            {
                ScaleX = 1.0,
                ScaleY = 1.0,
                TranslateX = 0,
                TranslateY = 0,
                ZoomCenter = new Point(100, 100)
            };

            service.PushState(state);
            Assert.True(service.CanGoBack);
        }

        [Fact]
        public void PopState_ReturnsLastPushedState()
        {
            var service = new MapNavigationService();
            var state1 = new ZoomState
            {
                ScaleX = 1.0,
                ScaleY = 1.0,
                TranslateX = 0,
                TranslateY = 0,
                ZoomCenter = new Point(100, 100)
            };
            var state2 = new ZoomState
            {
                ScaleX = 2.0,
                ScaleY = 2.0,
                TranslateX = 10,
                TranslateY = 10,
                ZoomCenter = new Point(200, 200)
            };

            service.PushState(state1);
            service.PushState(state2);

            var popped = service.PopState();
            Assert.NotNull(popped);
            Assert.Equal(state2.ScaleX, popped!.ScaleX);
            Assert.Equal(state2.ScaleY, popped!.ScaleY);
        }

        [Fact]
        public void PopState_DecreasesStackDepth()
        {
            var service = new MapNavigationService();
            var state1 = new ZoomState
            {
                ScaleX = 1.0,
                ScaleY = 1.0,
                TranslateX = 0,
                TranslateY = 0,
                ZoomCenter = new Point(100, 100)
            };
            var state2 = new ZoomState
            {
                ScaleX = 2.0,
                ScaleY = 2.0,
                TranslateX = 10,
                TranslateY = 10,
                ZoomCenter = new Point(200, 200)
            };

            service.PushState(state1);
            service.PushState(state2);
            Assert.True(service.CanGoBack);

            service.PopState();
            Assert.True(service.CanGoBack);

            service.PopState();
            Assert.False(service.CanGoBack);
        }

        [Fact]
        public void PopState_WhenEmpty_ReturnsNull()
        {
            var service = new MapNavigationService();
            var result = service.PopState();
            Assert.Null(result);
        }

        [Fact]
        public void PushState_WithNull_DoesNothing()
        {
            var service = new MapNavigationService();
            service.PushState(null!);
            Assert.False(service.CanGoBack);
        }

        [Fact]
        public void Clear_RemovesAllStates()
        {
            var service = new MapNavigationService();
            var state1 = new ZoomState
            {
                ScaleX = 1.0,
                ScaleY = 1.0,
                TranslateX = 0,
                TranslateY = 0,
                ZoomCenter = new Point(100, 100)
            };
            var state2 = new ZoomState
            {
                ScaleX = 2.0,
                ScaleY = 2.0,
                TranslateX = 10,
                TranslateY = 10,
                ZoomCenter = new Point(200, 200)
            };

            service.PushState(state1);
            service.PushState(state2);
            Assert.True(service.CanGoBack);

            service.Clear();
            Assert.False(service.CanGoBack);
        }

        [Fact]
        public void CanGoBack_ReflectsStackState()
        {
            var service = new MapNavigationService();
            var state = new ZoomState
            {
                ScaleX = 1.0,
                ScaleY = 1.0,
                TranslateX = 0,
                TranslateY = 0,
                ZoomCenter = new Point(100, 100)
            };

            Assert.False(service.CanGoBack);
            service.PushState(state);
            Assert.True(service.CanGoBack);
            service.PopState();
            Assert.False(service.CanGoBack);
        }

        [Fact]
        public void MultiplePushPop_MaintainsLifoOrder()
        {
            var service = new MapNavigationService();
            var state1 = new ZoomState
            {
                ScaleX = 1.0,
                ScaleY = 1.0,
                TranslateX = 0,
                TranslateY = 0,
                ZoomCenter = new Point(100, 100)
            };
            var state2 = new ZoomState
            {
                ScaleX = 2.0,
                ScaleY = 2.0,
                TranslateX = 10,
                TranslateY = 10,
                ZoomCenter = new Point(200, 200)
            };
            var state3 = new ZoomState
            {
                ScaleX = 3.0,
                ScaleY = 3.0,
                TranslateX = 20,
                TranslateY = 20,
                ZoomCenter = new Point(300, 300)
            };

            service.PushState(state1);
            service.PushState(state2);
            service.PushState(state3);

            var popped1 = service.PopState();
            var popped2 = service.PopState();
            var popped3 = service.PopState();

            Assert.NotNull(popped1);
            Assert.NotNull(popped2);
            Assert.NotNull(popped3);
            Assert.Equal(3.0, popped1!.ScaleX);
            Assert.Equal(2.0, popped2!.ScaleX);
            Assert.Equal(1.0, popped3!.ScaleX);
            Assert.Null(service.PopState());
        }
    }
}
