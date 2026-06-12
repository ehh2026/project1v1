namespace InteractiveWorldMap.Services
{
    public enum MarkerMouseDownAction
    {
        HandleNormalClick,
        AllowEditDrag,
        BlockNavigation
    }

    public static class MarkerMouseDownPolicy
    {
        public static MarkerMouseDownAction GetIndividualMarkerAction(bool isEditMode)
        {
            return isEditMode
                ? MarkerMouseDownAction.AllowEditDrag
                : MarkerMouseDownAction.HandleNormalClick;
        }

        public static MarkerMouseDownAction GetClusterMarkerAction(bool isEditMode)
        {
            return isEditMode
                ? MarkerMouseDownAction.BlockNavigation
                : MarkerMouseDownAction.HandleNormalClick;
        }
    }
}
