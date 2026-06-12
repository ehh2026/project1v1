using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class MarkerMouseDownPolicyTests
{
    [Fact]
    public void IndividualMarker_InEditMode_AllowsDragHandler()
    {
        var action = MarkerMouseDownPolicy.GetIndividualMarkerAction(isEditMode: true);

        Assert.Equal(MarkerMouseDownAction.AllowEditDrag, action);
    }

    [Fact]
    public void ClusterMarker_InEditMode_BlocksNavigation()
    {
        var action = MarkerMouseDownPolicy.GetClusterMarkerAction(isEditMode: true);

        Assert.Equal(MarkerMouseDownAction.BlockNavigation, action);
    }

    [Fact]
    public void IndividualMarker_OutsideEditMode_HandlesNormalClick()
    {
        var action = MarkerMouseDownPolicy.GetIndividualMarkerAction(isEditMode: false);

        Assert.Equal(MarkerMouseDownAction.HandleNormalClick, action);
    }
}
