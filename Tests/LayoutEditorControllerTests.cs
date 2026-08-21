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

    // Scope is now set by beginning a session rather than by a setter, so the tests that covered
    // that setter move with it. Two are kept because they cover a real past bug — variant identity
    // leaking between scopes — restated against sessions. Two are dropped, noted below, because
    // they described logic that no longer exists rather than behaviour that changed.

    [Fact]
    public void BeginEditSession_SetsTheScopeTheEditWillWriteTo()
    {
        var (ctrl, _, _, _) = Make();

        ctrl.BeginEditSession(SessionFor("key-abc"));

        Assert.Equal("key-abc", ctrl.ActiveSession!.LayoutKey);
    }

    // Dropped with the setter: SetLayoutKey_Null_ClearsKey. Ending a session is now the way to
    // leave a scope, covered by LayoutEditSessionTests.EndEditSession_ClearsTheSession.
    //
    // Dropped with the setter: SetLayoutKey_SameKey_PreservesActiveVariantIdentity. It pinned the
    // setter's same-key short-circuit, which only existed to avoid clearing identity on a no-op
    // write. Beginning a session always clears, and the editor immediately re-establishes identity
    // via LoadForEditSession, so there is no no-op case left to protect.

    [Fact]
    public void BeginEditSession_DoesNotInheritThePreviousSessionsVariantIdentity()
    {
        // Variant ids are only unique within a group, so identity crossing a scope change made
        // TrySave target a variant belonging to the previous layout. Scope can now only change by
        // beginning a session, which is where the clearing happens.
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-first"));
        ctrl.TrySave(OneExtension());
        Assert.Equal("manual-default", ctrl.ActiveVariantId);

        ctrl.BeginEditSession(SessionFor("key-second"));

        Assert.Null(ctrl.ActiveVariantId);
        Assert.Null(ctrl.ActiveVariantOrigin);
        Assert.Null(ctrl.ActiveVariantDisplayName);
    }

    [Fact]
    public void TrySave_AfterKeyChange_DoesNotWriteIntoPreviousKeysVariant()
    {
        // Regression: a stale active variant plus a changed key wrote the new scope's geometry
        // into the previous scope's named variant.
        var (ctrl, manager, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-origin"));
        ctrl.TrySaveAsVariant("Layout One", OneExtension());
        var originVariants = manager.ListVariants("key-origin");
        Assert.Single(originVariants);

        ctrl.BeginEditSession(SessionFor("key-elsewhere"));
        ctrl.TrySave(OneExtension());

        // The original group is untouched; the new group got its own default variant.
        Assert.Single(manager.ListVariants("key-origin"));
        Assert.Equal(originVariants[0].VariantId, manager.ListVariants("key-origin")[0].VariantId);
        Assert.Contains(manager.ListVariants("key-elsewhere"), v => v.VariantId == "manual-default");
    }


    // ─── Loading vs. adopting variant identity ────────────────────────────────
    //
    // TryLoad used to adopt the loaded layout's variant identity for whatever key it was handed,
    // so navigation's probe loads silently rewrote the editor's identity. Loading and adopting are
    // separate decisions now; these pin both halves, because the coupling is easy to reintroduce.

    [Fact]
    public void TryLoad_DoesNotAdoptVariantIdentity()
    {
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-elsewhere", "other-variant", "Other", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        ctrl.BeginEditSession(SessionFor("key-here"));
        ctrl.TrySave(OneExtension());
        var identityBefore = ctrl.ActiveVariantId;

        var loaded = ctrl.TryLoad("key-elsewhere");

        Assert.NotNull(loaded);
        Assert.Equal(identityBefore, ctrl.ActiveVariantId);
        Assert.NotEqual("other-variant", ctrl.ActiveVariantId);
    }

    [Fact]
    public void LoadForEditSession_AdoptsTheLoadedVariantIdentity()
    {
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-session", "named-variant", "Named", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        ctrl.BeginEditSession(SessionFor("key-session"));
        var loaded = ctrl.LoadForEditSession();

        Assert.NotNull(loaded);
        Assert.Equal("named-variant", ctrl.ActiveVariantId);
        Assert.Equal("Named", ctrl.ActiveVariantDisplayName);
        Assert.Equal(ManualLayoutOrigin.Manual, ctrl.ActiveVariantOrigin);
    }

    [Fact]
    public void SaveAfterLoadForEditSession_UpdatesTheLoadedVariantNotTheDefault()
    {
        // The reason editor entry loads unconditionally. Identity used to arrive as a side effect
        // of navigation; without establishing it here, a save would create or overwrite
        // "manual-default" and leave the variant the user was actually editing untouched.
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-session", "named-variant", "Named", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        ctrl.BeginEditSession(SessionFor("key-session"));
        ctrl.LoadForEditSession();
        ctrl.TrySave(OneExtension());

        var variants = manager.ListVariants("key-session");
        Assert.Single(variants);
        Assert.Equal("named-variant", variants[0].VariantId);
        Assert.DoesNotContain(variants, v => v.VariantId == "manual-default");
    }

    [Fact]
    public void LoadForEditSession_WithNoSession_ReturnsNullAndAdoptsNothing()
    {
        var (ctrl, manager, _, _) = Make();
        manager.SaveVariant(
            "key-orphan", "v", "V", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        var loaded = ctrl.LoadForEditSession();

        Assert.Null(loaded);
        Assert.Null(ctrl.ActiveVariantId);
    }

    [Fact]
    public void AdoptVariantIdentity_Null_LeavesIdentityUnchanged()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-keep"));
        ctrl.TrySave(OneExtension());
        var before = ctrl.ActiveVariantId;

        ctrl.AdoptVariantIdentity(null);

        Assert.Equal(before, ctrl.ActiveVariantId);
    }

    // ─── HasManualLayout (navigation precedence probe) ────────────────────────

    [Fact]
    public void HasManualLayout_WithSavedManualLayout_IsTrue()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-zoomed"));
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
    public void HasManualLayout_WhenAnExactSeedOnlyGroupWins_IsFalse()
    {
        // The probe must not promise a Manual layout the loader will not return. With an exact
        // AutoSeed-only group present, LoadLayout yields that seed; claiming "manual exists" from a
        // different compatible group would suppress the full-map fallback and display neither.
        var (ctrl, manager, _, _) = Make();
        const string exactKey = "hash1_z55.00_c10.00_10.00_s100x100_m3_p10.0_l50.0_n13.0";
        const string otherSize = "hash1_z55.00_c10.00_10.00_s200x200_m3_p10.0_l50.0_n13.0";

        manager.SaveVariant(
            otherSize, "hand-made", "Hand Made", ManualLayoutOrigin.Manual,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);
        manager.SaveVariant(
            exactKey, "seed-default", "Generated Seed", ManualLayoutOrigin.AutoSeed,
            OneExtension(), null, setAsDefault: true, setAsSelected: true);

        Assert.False(ctrl.HasManualLayout(exactKey));
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
        // Called during navigation, where mutating editor state would be wrong. Probing another
        // key must leave both the session's scope and its variant identity alone.
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-current"));
        ctrl.TrySave(OneExtension());
        var activeBefore = ctrl.ActiveVariantId;

        ctrl.HasManualLayout("some-other-key");

        Assert.Equal("key-current", ctrl.ActiveSession!.LayoutKey);
        Assert.Equal(activeBefore, ctrl.ActiveVariantId);
    }

    [Fact]
    public void SaveLayout_KeepsBackupOfPreviousFile()
    {
        var (ctrl, _, _, tempDir) = Make();
        var layoutPath = Path.Combine(tempDir, "layouts.json");
        ctrl.BeginEditSession(SessionFor("key-backup"));

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
        ctrl.BeginEditSession(SessionFor("key1"));
        Assert.Throws<ArgumentNullException>(() => ctrl.TrySave(null!));
    }

    [Fact]
    public void TrySave_NullKey_ReturnsFalseAndLogs()
    {
        var (ctrl, _, logger, _) = Make();
        // No edit session begun (default)
        var result = ctrl.TrySave(new List<RadialExtension>());
        Assert.False(result);
        Assert.NotEmpty(logger.WarningMessages);
    }

    [Fact]
    public void TrySave_ValidKey_ReturnsTrueAndSetsActive()
    {
        var (ctrl, _, _, tempDir) = Make();
        ctrl.BeginEditSession(SessionFor("key-save"));
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
        ctrl.BeginEditSession(SessionFor("key-save-event"));
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
        ctrl.BeginEditSession(SessionFor("key-del"));
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
        ctrl.BeginEditSession(SessionFor("key-del-event"));
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
        ctrl.BeginEditSession(SessionFor("key-load"));
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
        ctrl.BeginEditSession(SessionFor("key-exit-roundtrip"));
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
        ctrl.BeginEditSession(SessionFor("key-load-after-exit"));
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
    public void GetVariants_WithNoEditSession_ReturnsEmpty()
    {
        var (ctrl, _, _, _) = Make();

        Assert.Empty(ctrl.GetVariants());
    }

    [Fact]
    public void GetVariants_WithEditSession_ReturnsSavedVariants()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-variants"));
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
    public void SwitchToVariant_WithNoEditSession_ReturnsNull()
    {
        var (ctrl, _, _, _) = Make();

        Assert.Null(ctrl.SwitchToVariant("manual-default"));
    }

    [Fact]
    public void SwitchToVariant_WithMissingVariant_ReturnsNull()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-switch"));
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
        ctrl.BeginEditSession(SessionFor("key-switch-valid"));
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
        ctrl.BeginEditSession(SessionFor("key-save-as-null"));

        Assert.Throws<ArgumentNullException>(() => ctrl.TrySaveAsVariant("Variant", null!));
    }

    [Fact]
    public void TrySaveAsVariant_WithNoEditSession_ReturnsFalse()
    {
        var (ctrl, _, logger, _) = Make();

        var saved = ctrl.TrySaveAsVariant("Variant", new List<RadialExtension>());

        Assert.False(saved);
        Assert.Contains(logger.WarningMessages, message => message.Contains("no active edit session"));
    }

    [Fact]
    public void TrySaveAsVariant_WithBlankName_ReturnsFalse()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-save-as-blank"));

        var saved = ctrl.TrySaveAsVariant("  ", new List<RadialExtension>());

        Assert.False(saved);
    }

    [Fact]
    public void TrySaveAsVariant_WithName_SlugifiesVariantIdAndRaisesEvent()
    {
        var (ctrl, _, _, _) = Make();
        ctrl.BeginEditSession(SessionFor("key-save-as"));
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
        ctrl.BeginEditSession(SessionFor("key-delete-active"));
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
