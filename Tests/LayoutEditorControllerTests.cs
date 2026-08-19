using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Tests.TestHelpers;
using Xunit;

using static InteractiveWorldMap.Tests.TestHelpers.LayoutEditorTestFixtures;

namespace InteractiveWorldMap.Tests;

/// <summary>
/// Unit tests for <see cref="LayoutEditorController"/>.
/// </summary>
public class LayoutEditorControllerTests
{
    // ─── Constructor guards ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullLayoutManager_Throws()
    {
        var log = new MockLogger();
        var config = new VisualConfig();
        Assert.Throws<ArgumentNullException>(() =>
            new LayoutEditorController(null!, config, log));
    }

    [Fact]
    public void Constructor_NullVisualConfig_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-lec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var manager = new ManualLayoutManager(Path.Combine(tempDir, "l.json"), new MockLogger());
        Assert.Throws<ArgumentNullException>(() =>
            new LayoutEditorController(manager, null!, new MockLogger()));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iwm-lec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var manager = new ManualLayoutManager(Path.Combine(tempDir, "l.json"), new MockLogger());
        Assert.Throws<ArgumentNullException>(() =>
            new LayoutEditorController(manager, new VisualConfig(), null!));
    }

    // ─── State transitions ────────────────────────────────────────────────────

    [Fact]
    public void IsEditMode_DefaultsFalse()
    {
        var (ctrl, _, _, _) = Make();
        Assert.False(ctrl.IsEditMode);
    }

    [Fact]
    public void EnterEditMode_SetsIsEditModeTrue()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.EnterEditMode();
        Assert.True(ctrl.IsEditMode);
    }

    [Fact]
    public void EnterEditMode_RaisesEditModeEntered()
    {
        var (ctrl, _, _, _) = Make();
        var raised = false;
        ctrl.EditModeEntered += () => raised = true;

        ctrl.EnterEditMode();

        Assert.True(raised);
    }

    [Fact]
    public void ExitEditMode_SetsIsEditModeFalse()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.EnterEditMode();
        ctrl.ExitEditMode();
        Assert.False(ctrl.IsEditMode);
    }

    [Fact]
    public void ExitEditMode_RaisesEditModeExited()
    {
        var (ctrl, _, _, _) = Make();
        var raised = false;
        ctrl.EditModeExited += () => raised = true;
        ctrl.EnterEditMode();

        ctrl.ExitEditMode();

        Assert.True(raised);
    }

    [Fact]
    public void SetLayoutKey_UpdatesCurrentLayoutKey()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-abc");
        Assert.Equal("key-abc", ctrl.CurrentLayoutKey);
    }

    [Fact]
    public void SetLayoutKey_Null_ClearsKey()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-abc");
        ctrl.SetLayoutKey(null);
        Assert.Null(ctrl.CurrentLayoutKey);
    }

    [Fact]
    public void SetLayoutKey_ChangingKey_ClearsActiveVariantIdentity()
    {
        // Variant ids are only unique within a group. Carrying one across a scope change makes
        // TrySave target a variant belonging to the previous layout.
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-first");
        ctrl.TrySave(OneExtension());
        Assert.Equal("manual-default", ctrl.ActiveVariantId);

        ctrl.SetLayoutKey("key-second");

        Assert.Null(ctrl.ActiveVariantId);
        Assert.Null(ctrl.ActiveVariantOrigin);
        Assert.Null(ctrl.ActiveVariantDisplayName);
    }

    [Fact]
    public void SetLayoutKey_SameKey_PreservesActiveVariantIdentity()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-same");
        ctrl.TrySave(OneExtension());

        ctrl.SetLayoutKey("key-same");

        Assert.Equal("manual-default", ctrl.ActiveVariantId);
        Assert.Equal(ManualLayoutOrigin.Manual, ctrl.ActiveVariantOrigin);
    }

    [Fact]
    public void TrySave_AfterKeyChange_DoesNotWriteIntoPreviousKeysVariant()
    {
        // Regression: a stale active variant plus a changed key wrote the new scope's geometry
        // into the previous scope's named variant.
        var (ctrl, manager, _, _) = Make();
        ctrl.SetLayoutKey("key-origin");
        ctrl.TrySaveAsVariant("Layout One", OneExtension());
        var originVariants = manager.ListVariants("key-origin");
        Assert.Single(originVariants);

        ctrl.SetLayoutKey("key-elsewhere");
        ctrl.TrySave(OneExtension());

        // The original group is untouched; the new group got its own default variant.
        Assert.Single(manager.ListVariants("key-origin"));
        Assert.Equal(originVariants[0].VariantId, manager.ListVariants("key-origin")[0].VariantId);
        Assert.Contains(manager.ListVariants("key-elsewhere"), v => v.VariantId == "manual-default");
    }


    // ─── HasManualLayout (navigation precedence probe) ────────────────────────

    [Fact]
    public void HasManualLayout_WithSavedManualLayout_IsTrue()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-zoomed");
        ctrl.TrySave(OneExtension());

        Assert.True(ctrl.HasManualLayout("key-zoomed"));
    }

    [Fact]
    public void HasManualLayout_WithOnlyAutoSeed_IsFalse()
    {
        // A seed is a starting point, not a decision, so it must not outrank a hand-made
        // full-map layout when zooming into a single location.
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-seeded", "seed-default", "Generated Seed", ManualLayoutOrigin.AutoSeed,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        Assert.False(ctrl.HasManualLayout("key-seeded"));
    }

    [Fact]
    public void HasManualLayout_WithSelectedSeedMaskingAManualVariant_IsStillTrue()
    {
        // Regression: the probe used LoadLayout, which returns the *selected* variant. Selecting
        // the seed hid the Manual variant beside it, so navigation precedence wrongly fell back to
        // the full-map layout. The question is about the group, not the current selection.
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-mixed", "hand-made", "Hand Made", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: false, setAsSelected: false);
        manager.SaveVariant(
            "key-mixed", "seed-default", "Generated Seed", ManualLayoutOrigin.AutoSeed,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        Assert.True(ctrl.HasManualLayout("key-mixed"));
    }

    [Fact]
    public void HasManualLayout_WithNoLayout_IsFalse()
    {
        var (ctrl, _, _, _) = Make();
        Assert.False(ctrl.HasManualLayout("key-absent"));
        Assert.False(ctrl.HasManualLayout(null));
        Assert.False(ctrl.HasManualLayout(""));
    }

    [Fact]
    public void HasManualLayout_DoesNotDisturbActiveVariantState()
    {
        // Called during navigation, where mutating editor state would be wrong. TryLoad has that
        // side effect; this probe must not.
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-current");
        ctrl.TrySave(OneExtension());
        var activeBefore = ctrl.ActiveVariantId;

        ctrl.HasManualLayout("some-other-key");

        Assert.Equal("key-current", ctrl.CurrentLayoutKey);
        Assert.Equal(activeBefore, ctrl.ActiveVariantId);
    }

    [Fact]
    public void SaveLayout_KeepsBackupOfPreviousFile()
    {
        var (ctrl, _, _, tempDir) = Make();
        var layoutPath = Path.Combine(tempDir, "layouts.json");
        ctrl.SetLayoutKey("key-backup");

        ctrl.TrySave(OneExtension());
        Assert.True(File.Exists(layoutPath));
        Assert.False(File.Exists(layoutPath + ".bak"), "No backup expected before a second save.");

        ctrl.TrySave(OneExtension());

        Assert.True(File.Exists(layoutPath + ".bak"),
            "The previous layout file must be recoverable after an overwriting save.");
    }

    [Fact]
    public void SetManualLayoutActive_True_SetsFlag()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetManualLayoutActive(true);
        Assert.True(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void SetManualLayoutActive_False_ClearsFlag()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetManualLayoutActive(true);
        ctrl.SetManualLayoutActive(false);
        Assert.False(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void IsManualLayoutSuppressed_DefaultsFalse()
    {
        var (ctrl, _, _, _) = Make();
        Assert.False(ctrl.IsManualLayoutSuppressed);
    }

    [Fact]
    public void UnloadManualLayout_SuppressesAndDeactivates()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetManualLayoutActive(true);

        ctrl.UnloadManualLayout();

        Assert.True(ctrl.IsManualLayoutSuppressed);
        Assert.False(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void SetManualLayoutActive_True_ClearsSuppression()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.UnloadManualLayout();
        Assert.True(ctrl.IsManualLayoutSuppressed);

        ctrl.SetManualLayoutActive(true);

        Assert.False(ctrl.IsManualLayoutSuppressed);
        Assert.True(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void SetManualLayoutActive_False_LeavesSuppressionUntouched()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.UnloadManualLayout();

        // Auto-apply paths call SetManualLayoutActive(false) when no layout is found; that must not
        // clear a session unload.
        ctrl.SetManualLayoutActive(false);

        Assert.True(ctrl.IsManualLayoutSuppressed);
    }

    // ─── TrySave ─────────────────────────────────────────────────────────────

    [Fact]
    public void TrySave_NullExtensions_Throws()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key1");
        Assert.Throws<ArgumentNullException>(() => ctrl.TrySave(null!));
    }

    [Fact]
    public void TrySave_NullKey_ReturnsFalseAndLogs()
    {
        var (ctrl, _, logger, _) = Make();
        // CurrentLayoutKey is null (default)
        var result = ctrl.TrySave(new List<RadialExtension>());
        Assert.False(result);
        Assert.NotEmpty(logger.WarningMessages);
    }

    [Fact]
    public void TrySave_ValidKey_ReturnsTrueAndSetsActive()
    {
        var (ctrl, _, _, tempDir) = Make();
        ctrl.SetLayoutKey("key-save");
        var ext = new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("x"),
                OriginalPosition = new Point(10, 10),
                ExtendedPosition = new Point(50, 50),
                Angle            = 45.0
            }
        };
        bool saved = ctrl.TrySave(ext);
        Assert.True(saved);
        Assert.True(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void TrySave_ValidKey_RaisesManualLayoutActivityChanged()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-save-event");
        bool? activeState = null;
        ctrl.ManualLayoutActivityChanged += active => activeState = active;

        bool saved = ctrl.TrySave(new List<RadialExtension>
        {
            new RadialExtension
            {
                Location = Loc("event"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(20, 20),
                Angle = 45.0
            }
        });

        Assert.True(saved);
        Assert.True(activeState);
    }

    // ─── TryDelete ────────────────────────────────────────────────────────────

    [Fact]
    public void TryDelete_NullKey_ReturnsFalseAndLogs()
    {
        var (ctrl, _, logger, _) = Make();
        var result = ctrl.TryDelete();
        Assert.False(result);
        Assert.NotEmpty(logger.WarningMessages);
    }

    [Fact]
    public void TryDelete_AfterSave_ReturnsTrueAndClearsActive()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-del");
        var ext = new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("y"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle            = 0.0
            }
        };
        ctrl.TrySave(ext);
        Assert.True(ctrl.IsManualLayoutActive);

        bool deleted = ctrl.TryDelete();
        Assert.True(deleted);
        Assert.False(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void TryDelete_AfterSave_RaisesManualLayoutActivityChanged()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-del-event");
        ctrl.TrySave(new List<RadialExtension>
        {
            new RadialExtension
            {
                Location = Loc("event"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(20, 20),
                Angle = 45.0
            }
        });
        bool? activeState = null;
        ctrl.ManualLayoutActivityChanged += active => activeState = active;

        bool deleted = ctrl.TryDelete();

        Assert.True(deleted);
        Assert.False(activeState);
    }

    // ─── TryLoad ─────────────────────────────────────────────────────────────

    [Fact]
    public void TryLoad_MissingKey_ReturnsNull()
    {
        var (ctrl, _, _, _) = Make();
        var layout = ctrl.TryLoad("nonexistent");
        Assert.Null(layout);
    }

    [Fact]
    public void TryLoad_AfterSave_ReturnsLayout()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-load");
        var ext = new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("z"),
                OriginalPosition = new Point(5, 5),
                ExtendedPosition = new Point(55, 55),
                Angle            = 90.0
            }
        };
        ctrl.TrySave(ext);

        var loaded = ctrl.TryLoad("key-load");
        Assert.NotNull(loaded);
    }

    // ─── ExitEditMode manual-layout replay prerequisites ─────────────────────
    // These tests verify the controller-level invariants that MainWindow.ExitEditMode
    // relies on when deciding to replay the manual layout (the Phase 1 fix).

    [Fact]
    public void ExitEditMode_AfterTrySave_IsManualLayoutActiveRemainsTrue()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-exit-roundtrip");
        ctrl.TrySave(new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("a"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle            = 45.0
            }
        });
        Assert.True(ctrl.IsManualLayoutActive);

        ctrl.ExitEditMode();

        // IsManualLayoutActive must survive ExitEditMode so MainWindow's branch fires.
        Assert.True(ctrl.IsManualLayoutActive);
    }

    [Fact]
    public void TryLoad_AfterSaveAndExitEditMode_ReturnsLayout()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-load-after-exit");
        ctrl.TrySave(new List<RadialExtension>
        {
            new RadialExtension
            {
                Location         = Loc("b"),
                OriginalPosition = new Point(5, 5),
                ExtendedPosition = new Point(50, 50),
                Angle            = 90.0
            }
        });
        ctrl.ExitEditMode();

        // TryLoad must return the saved layout so MainWindow's ExitEditMode branch can replay it.
        var layout = ctrl.TryLoad("key-load-after-exit");
        Assert.NotNull(layout);
    }


    [Fact]
    public void GetVariants_WithNoCurrentLayoutKey_ReturnsEmpty()
    {
        var (ctrl, _, _, _) = Make();

        Assert.Empty(ctrl.GetVariants());
    }

    [Fact]
    public void GetVariants_WithCurrentLayoutKey_ReturnsSavedVariants()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-variants");
        Assert.True(ctrl.TrySave(new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        }));

        var variants = ctrl.GetVariants();

        var variant = Assert.Single(variants);
        Assert.Equal("manual-default", variant.VariantId);
        Assert.True(variant.IsSelected);
    }

    [Fact]
    public void SwitchToVariant_WithNoCurrentLayoutKey_ReturnsNull()
    {
        var (ctrl, _, _, _) = Make();

        Assert.Null(ctrl.SwitchToVariant("manual-default"));
    }

    [Fact]
    public void SwitchToVariant_WithMissingVariant_ReturnsNull()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-switch");
        Assert.True(ctrl.TrySave(new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        }));

        Assert.Null(ctrl.SwitchToVariant("missing"));
    }

    [Fact]
    public void SwitchToVariant_WithValidVariant_UpdatesActiveVariant()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-switch-valid");
        var extensions = new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        };
        Assert.True(ctrl.TrySave(extensions));
        Assert.True(ctrl.TrySaveAsVariant("Wide Variant", extensions));
        var variantId = ctrl.ActiveVariantId!;
        Assert.NotEqual("manual-default", variantId);
        Assert.NotNull(ctrl.SwitchToVariant("manual-default"));

        var loaded = ctrl.SwitchToVariant(variantId);

        Assert.NotNull(loaded);
        Assert.Equal(variantId, ctrl.ActiveVariantId);
        Assert.Equal(ManualLayoutOrigin.Manual, ctrl.ActiveVariantOrigin);
        Assert.Equal("Wide Variant", ctrl.ActiveVariantDisplayName);
    }

    [Fact]
    public void TrySaveAsVariant_WithNullExtensions_Throws()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-save-as-null");

        Assert.Throws<ArgumentNullException>(() => ctrl.TrySaveAsVariant("Variant", null!));
    }

    [Fact]
    public void TrySaveAsVariant_WithNoCurrentLayoutKey_ReturnsFalse()
    {
        var (ctrl, _, logger, _) = Make();

        var saved = ctrl.TrySaveAsVariant("Variant", new List<RadialExtension>());

        Assert.False(saved);
        Assert.Contains(logger.WarningMessages, message => message.Contains("CurrentLayoutKey"));
    }

    [Fact]
    public void TrySaveAsVariant_WithBlankName_ReturnsFalse()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-save-as-blank");

        var saved = ctrl.TrySaveAsVariant("  ", new List<RadialExtension>());

        Assert.False(saved);
    }

    [Fact]
    public void TrySaveAsVariant_WithName_SlugifiesVariantIdAndRaisesEvent()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-save-as");
        var events = 0;
        ctrl.VariantsChanged += _ => events++;
        var extensions = new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        };

        var saved = ctrl.TrySaveAsVariant("Wide Variant!! Name With Extra Text", extensions);

        Assert.True(saved);
        Assert.StartsWith("wide-variant-name-w", ctrl.ActiveVariantId);
        Assert.Equal("Wide Variant!! Name With Extra Text", ctrl.ActiveVariantDisplayName);
        Assert.True(ctrl.IsManualLayoutActive);
        Assert.Equal(1, events);
    }

    [Fact]
    public void TryDeleteActiveVariant_WithNoActiveVariant_ReturnsFalse()
    {
        var (ctrl, _, logger, _) = Make();

        Assert.False(ctrl.TryDeleteActiveVariant());
        Assert.Contains(logger.WarningMessages, message => message.Contains("no active variant"));
    }

    [Fact]
    public void TryDeleteActiveVariant_WithSavedManualVariant_DeletesAndRaisesEvent()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.SetLayoutKey("key-delete-active");
        var events = 0;
        ctrl.VariantsChanged += _ => events++;
        var extensions = new List<RadialExtension>
        {
            new()
            {
                Location = Loc("alpha"),
                OriginalPosition = new Point(0, 0),
                ExtendedPosition = new Point(30, 30),
                Angle = 45
            }
        };
        Assert.True(ctrl.TrySave(extensions));
        Assert.True(ctrl.TrySaveAsVariant("Variant B", extensions));

        var deleted = ctrl.TryDeleteActiveVariant();

        Assert.True(deleted);
        Assert.Equal("manual-default", ctrl.ActiveVariantId);
        Assert.True(ctrl.IsManualLayoutActive);
        Assert.Equal(3, events);
    }
}
