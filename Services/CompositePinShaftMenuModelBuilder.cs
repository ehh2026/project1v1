using System;
using System.Collections.Generic;
using System.Linq;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Builds the ordered list of shaft candidates for the right-click override context menu.
    /// Views stay free of Services — MainWindow maps the returned DTOs to WPF menu items.
    /// </summary>
    public class CompositePinShaftMenuModelBuilder
    {
        private readonly PinPartPlacementCalculator _calculator;

        public CompositePinShaftMenuModelBuilder(PinPartPlacementCalculator calculator)
        {
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }

        /// <summary>
        /// Returns shaft candidates ranked by fit score for <paramref name="target"/>.
        /// The currently active pair is marked with <see cref="CompositePinShaftMenuItem.IsSelected"/>.
        /// </summary>
        public IReadOnlyList<CompositePinShaftMenuItem> BuildMenuItems(
            PinPlacementTarget target,
            IReadOnlyDictionary<string, PinPartGeometryEntry> candidates,
            PinPartConfig config,
            string? currentPairId)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var ranked = _calculator.ScoreAll(target, candidates, config);
            return ranked
                .Select(r => new CompositePinShaftMenuItem(
                    r.PairId,
                    $"{r.PairId}  (score {r.Score:F1}\u00b0)",
                    r.Score,
                    string.Equals(r.PairId, currentPairId, StringComparison.Ordinal)))
                .ToList();
        }
    }
}
