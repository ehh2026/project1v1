using System.Collections.Generic;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Manages navigation state for map zoom operations.
    /// </summary>
    public class MapNavigationService
    {
        private readonly Stack<ZoomState> _navigationStack = new Stack<ZoomState>();

        /// <summary>
        /// Gets whether the user can navigate back to a previous state.
        /// </summary>
        public bool CanGoBack => _navigationStack.Count > 0;

        /// <summary>
        /// Pushes a new zoom state onto the navigation stack.
        /// </summary>
        /// <param name="state">The state to save</param>
        public void PushState(ZoomState state)
        {
            if (state != null)
            {
                _navigationStack.Push(state);
            }
        }

        /// <summary>
        /// Pops the most recent zoom state from the navigation stack.
        /// </summary>
        /// <returns>The previous state, or null if stack is empty</returns>
        public ZoomState? PopState()
        {
            return _navigationStack.Count > 0 ? _navigationStack.Pop() : null;
        }

        /// <summary>
        /// Clears all navigation history.
        /// </summary>
        public void Clear()
        {
            _navigationStack.Clear();
        }
    }
}
