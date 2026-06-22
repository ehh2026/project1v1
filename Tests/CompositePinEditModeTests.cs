using System;
using System.IO;
using System.Linq;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class CompositePinEditModeTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    // ─── Architectural: CanUseCompositePins no longer gates on IsEditMode ───

    [Fact]
    public void CanUseCompositePins_DoesNotGateOnIsEditMode()
    {
        var mainWindowPath = Path.Combine(RepoRoot, "MainWindow.CompositePins.partial.cs");
        var source = File.ReadAllText(mainWindowPath);

        // Extract the CanUseCompositePins method body
        var methodStart = source.IndexOf("private bool CanUseCompositePins()");
        Assert.True(methodStart >= 0, "CanUseCompositePins method not found");
        var methodBodyStart = source.IndexOf("{", methodStart);
        var methodBodyEnd = source.IndexOf("}", methodBodyStart);
        var methodBody = source.Substring(methodBodyStart, methodBodyEnd - methodBodyStart + 1);

        // The method body should NOT contain an edit-mode gate
        Assert.DoesNotContain("!_layoutEditor.IsEditMode", methodBody);
        Assert.DoesNotContain("!IsEditMode", methodBody);
    }

    // ─── LayoutEditorController.BuildExtensions with composite-pin endpoints ───

    [Fact]
    public void BuildExtensions_CompositePinEndpoint_ComputesCorrectAngleAndLength()
    {
        var location = new Location { Name = "Test", PixelX = 100, PixelY = 100 };
        var originalScreen = new Point(200, 300);
        var endpoint = new Point(200, 100); // straight up, 200 px

        var extensions = LayoutEditorController.BuildExtensions(new[]
        {
            (location, MarkerCenter: endpoint, OriginalScreen: originalScreen)
        });

        Assert.Single(extensions);
        var ext = extensions[0];
        Assert.Equal(200, ext.ExtendedPosition.X, 0);
        Assert.Equal(100, ext.ExtendedPosition.Y, 0);
        Assert.Equal(200, ext.OriginalPosition.X, 0);
        Assert.Equal(300, ext.OriginalPosition.Y, 0);

        // Angle: Atan2(dx, -dy) = Atan2(0, -(-200)) = Atan2(0, 200) = 0 degrees
        Assert.Equal(0, ext.Angle, 1);
    }

    [Fact]
    public void BuildExtensions_CompositePinEndpoint_Rightward_ComputesCorrectAngle()
    {
        var location = new Location { Name = "Test", PixelX = 100, PixelY = 100 };
        var originalScreen = new Point(200, 300);
        var endpoint = new Point(300, 300); // right, 100 px

        var extensions = LayoutEditorController.BuildExtensions(new[]
        {
            (location, MarkerCenter: endpoint, OriginalScreen: originalScreen)
        });

        Assert.Single(extensions);
        var ext = extensions[0];

        // Angle: Atan2(dx, -dy) = Atan2(100, 0) = 90 degrees
        Assert.Equal(90, ext.Angle, 1);
    }

    // ─── CompositePinTargetBuilder stub target for edit mode ───

    [Fact]
    public void Build_StubTarget_ScreenUpByDefaultStubLength()
    {
        var builder = new CompositePinTargetBuilder();
        var location = new Location { Name = "Test", PixelX = 100, PixelY = 100 };
        var viewport = new ViewportState
        {
            ViewportX = 0,
            ViewportY = 0,
            ViewportWidth = 200,
            ViewportHeight = 200,
            ZoomLevel = 1.0
        };
        var config = new PinPartConfig
        {
            DefaultStubLengthPixels = 24
        };

        var target = builder.Build(location, viewport, 400, 300, config);

        var expectedStart = viewport.SourceToScreen(100, 100, 400, 300);
        Assert.Equal(expectedStart.X, target.StartScreen.X, 1);
        Assert.Equal(expectedStart.Y, target.StartScreen.Y, 1);
        Assert.Equal(expectedStart.X, target.EndScreen.X, 1);
        Assert.Equal(expectedStart.Y - 24, target.EndScreen.Y, 1);
    }

    // ─── M4: ExtensionLineRenderer.Apply must not untrack guide lines in the composite-success branch ───

    [Fact]
    public void ExtensionLineRenderer_Apply_CompositeSuccessBranch_DoesNotUntrackMarker()
    {
        var rendererSource = File.ReadAllText(Path.Combine(RepoRoot, "Views", "ExtensionLineRenderer.cs"));

        // Locate the Apply method's composite-success branch (after tryCompositePinApplier returns true).
        var applierCallIdx = rendererSource.IndexOf("tryCompositePinApplier(marker, originalScreenPos, extendedScreenPos)",
            StringComparison.Ordinal);
        Assert.True(applierCallIdx >= 0, "tryCompositePinApplier call not found in Apply.");

        // The two bare Remove calls must not appear after the applier invocation.
        var applySuccessBranch = rendererSource.Substring(applierCallIdx, 400);
        Assert.DoesNotContain("_markerToLine.Remove(marker)", applySuccessBranch);
        Assert.DoesNotContain("_markerToPinLines.Remove(marker)", applySuccessBranch);
    }

    // ─── CompositePinRenderPlan head center fallback for endpoint extraction ───

    [Fact]
    public void CompositePinRenderPlan_HeadCenterLocal_IsSet_InRealPlan()
    {
        var builder = new CompositePinRenderPlanBuilder();
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(100, 320),
            EndScreen = new Point(100, 100),
            LocationId = "loc",
            GroupId = 1
        };
        var placement = new PinPartPlacementResult
        {
            PairId = "pin_a",
            PairGeometry = new PinPartGeometryEntry
            {
                ShaftFile = "pin_01_shaft.png",
                HeadFile = "pin_01_head.png",
                Shaft = new PinPartShaftGeometry
                {
                    ImageSize = new PinPartImageSize { Width = 20, Height = 100 },
                    LocalTip = new PinPartPoint { X = 10, Y = 20 },
                    LocalJoin = new PinPartPoint { X = 10, Y = 5 },
                    NativeAngleDeg = 0,
                    NativeLength = 100,
                    NativeShaftHalfWidthPx = 2,
                    Segmentation = new PinPartShaftSegmentation
                    {
                        TipCapLength = 5,
                        HeadCapLength = 5,
                        StretchStartDistance = 10,
                        StretchEndDistance = 90,
                        StretchableLength = 80,
                        MinimumMiddleRatio = 0.5
                    }
                },
                Head = new PinPartHeadGeometry
                {
                    ImageSize = new PinPartImageSize { Width = 20, Height = 20 },
                    LocalCenter = new PinPartPoint { X = 10, Y = 0 },
                    StubDirectionDeg = 0,
                    LocalRadius = 8
                }
            },
            TargetAngleDeg = 0.0,
            TargetLengthPx = 220.0
        };
        var config = new PinPartConfig
        {
            PartsFolderPath = "Pins_v2/parts",
            UseLitShafts = true
        };

        var plan = builder.BuildPlan(target, placement, config);

        Assert.NotNull(plan);
        Assert.NotEqual(new Point(0, 0), plan.HeadCenterLocal);
    }
}
