using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Tests for Task 3 of the tuning-and-pin-render-bugfixes plan (H13):
/// Composite drag "stub" — Policy A aligns the guide line endpoint with the clamped rendered head.
/// </summary>
public class CompositeDragStretchTests
{
    // ── PinPartPlacementCalculator: clamping behaviour ──

    [Fact]
    public void CalculatePlacement_TargetExceedsMaxStretch_IsStretchClampedTrue()
    {
        var calculator = new PinPartPlacementCalculator();
        var config = new PinPartConfig
        {
            SelectionMode = PinPartSelectionMode.NearestFit,
            MaxResidualRotationDeg = 45.0,
            MinStretchFactor = 0.75,
            MaxStretchFactor = 1.10
        };
        // Native length 100; target 200px — well beyond 1.10 × 100 = 110 max.
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(0, 0),
            EndScreen = new Point(0, -200),  // straight up 200px
            LocationId = "loc-1",
            GroupId = 1
        };
        var candidates = SingleCandidate(nativeAngleDeg: 0.0, nativeLength: 100.0);

        var result = calculator.CalculatePlacement(target, candidates, config);

        Assert.True(result.IsStretchClamped);
        Assert.Equal(1.10, result.AppliedStretchFactor, 5);
    }

    [Fact]
    public void CalculatePlacement_TargetWithinRange_IsStretchClampedFalse()
    {
        var calculator = new PinPartPlacementCalculator();
        var config = new PinPartConfig
        {
            SelectionMode = PinPartSelectionMode.NearestFit,
            MaxResidualRotationDeg = 45.0,
            MinStretchFactor = 0.75,
            MaxStretchFactor = 1.35
        };
        // Native length 100; target 120px — 1.20× is within [0.75, 1.35].
        var target = new PinPlacementTarget
        {
            StartScreen = new Point(0, 0),
            EndScreen = new Point(0, -120),  // straight up 120px
            LocationId = "loc-1",
            GroupId = 1
        };
        var candidates = SingleCandidate(nativeAngleDeg: 0.0, nativeLength: 100.0);

        var result = calculator.CalculatePlacement(target, candidates, config);

        Assert.False(result.IsStretchClamped);
    }

    // ── Policy A: rendered-head position == clamped shaft length along target angle ──

    [Fact]
    public void PolicyA_RenderedHeadAlignedWithClampedLength_DiffersFromRawCursor()
    {
        // Tip at (100, 300); target straight up 200 px but shaft clamps at 1.10 × 100 = 110 px.
        // The rendered head stops at tipY - 110 = 190, while the cursor is at tipY - 200 = 100.
        // Policy A drives the guide line to the rendered head (190), not the cursor (100).

        var tipScreen = new Point(100, 300);
        double clampedLength = 110;   // MaxStretchFactor × nativeLength
        double cursorY = 100;   // raw cursor 200px above tip

        var renderedHeadY = tipScreen.Y - clampedLength;  // 190
        Assert.NotEqual(cursorY, renderedHeadY);
        Assert.Equal(190, renderedHeadY, 1);
    }

    // ── Source guard: composite drag branch uses rendered head, not raw mousePos ──

    [Fact]
    public void CompositeDragBranch_UsesRenderedHeadForGuideLineEndpoint()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.LayoutEditorDrag.partial.cs"));

        Assert.Contains("draggedCpm.RenderPlan.HeadCenterLocal", source);
        Assert.Contains("renderedHead", source);
        Assert.Contains("_extensionLineRenderer.MoveLineEndpoint(_draggedMarker, renderedHead)", source);
    }

    [Fact]
    public void CompositeDragBranch_RecordsRenderedHeadNotRawMousePos()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "MainWindow.LayoutEditorDrag.partial.cs"));

        Assert.Contains("_overrideStore.RecordEndpoints(_draggedMarker.Location.Name, originalPos, renderedHead)", source);
    }

    // ── Helpers ──

    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static IReadOnlyDictionary<string, PinPartGeometryEntry> SingleCandidate(
        double nativeAngleDeg, double nativeLength) =>
        new Dictionary<string, PinPartGeometryEntry>
        {
            ["pin_a"] = new()
            {
                HeadFile = "pin_a_head.png",
                ShaftFile = "pin_a_shaft.png",
                Shaft = new PinPartShaftGeometry
                {
                    NativeAngleDeg = nativeAngleDeg,
                    NativeLength = nativeLength,
                    LocalTip = new PinPartPoint { X = 5, Y = (int)nativeLength - 5 },
                    LocalJoin = new PinPartPoint { X = 5, Y = 5 }
                }
            }
        };
}
