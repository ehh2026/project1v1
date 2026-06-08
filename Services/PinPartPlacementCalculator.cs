using System;
using System.Collections.Generic;
using System.Linq;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Chooses the closest pin-part pair for a target segment and computes residual transforms.
    /// </summary>
    public class PinPartPlacementCalculator
    {
        public PinPartPlacementResult CalculatePlacement(
            PinPlacementTarget target,
            IReadOnlyDictionary<string, PinPartGeometryEntry> candidates,
            PinPartConfig config,
            string? preferredPairId = null)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (candidates.Count == 0)
                throw new ArgumentException("At least one pin-part candidate is required.", nameof(candidates));

            var targetAngle = GetAngleDegrees(target.StartScreen.X, target.StartScreen.Y, target.EndScreen.X, target.EndScreen.Y);
            var targetLength = GetDistance(target.StartScreen.X, target.StartScreen.Y, target.EndScreen.X, target.EndScreen.Y);

            // Honour saved pair id when it still exists in the current candidate set.
            if (preferredPairId != null && candidates.TryGetValue(preferredPairId, out var preferred))
                return BuildPlacementResult(preferredPairId, preferred, targetAngle, targetLength, config);

            var ranked = candidates
                .Select(candidate => BuildPlacementResult(candidate.Key, candidate.Value, targetAngle, targetLength, config))
                .OrderBy(result => result.Score)
                .ThenBy(result => result.PairId, StringComparer.Ordinal)
                .ToList();

            return ranked[0];
        }

        private static PinPartPlacementResult BuildPlacementResult(
            string pairId,
            PinPartGeometryEntry geometry,
            double targetAngle,
            double targetLength,
            PinPartConfig config)
        {
            var nativeAngle = geometry.Shaft.NativeAngleDeg;
            var nativeLength = geometry.Shaft.NativeLength;
            var requestedRotation = NormalizeSignedAngle(targetAngle - nativeAngle);
            var requestedStretch = nativeLength <= 0.0 ? 1.0 : targetLength / nativeLength;

            var appliedRotation = requestedRotation;
            var appliedStretch = requestedStretch;
            var isRotationClamped = false;
            var isStretchClamped = false;

            if (config.SelectionMode == PinPartSelectionMode.NearestFit)
            {
                var maxRotation = Math.Abs(config.MaxResidualRotationDeg);
                if (Math.Abs(appliedRotation) > maxRotation)
                {
                    appliedRotation = Math.Sign(appliedRotation) * maxRotation;
                    isRotationClamped = true;
                }

                var minStretch = Math.Min(config.MinStretchFactor, config.MaxStretchFactor);
                var maxStretch = Math.Max(config.MinStretchFactor, config.MaxStretchFactor);
                if (appliedStretch < minStretch)
                {
                    appliedStretch = minStretch;
                    isStretchClamped = true;
                }
                else if (appliedStretch > maxStretch)
                {
                    appliedStretch = maxStretch;
                    isStretchClamped = true;
                }
            }

            var angleError = Math.Abs(requestedRotation);
            var lengthError = nativeLength <= 0.0 ? targetLength : Math.Abs(targetLength - nativeLength) / nativeLength;
            var score = angleError + (lengthError * 100.0);

            return new PinPartPlacementResult
            {
                PairId = pairId,
                PairGeometry = geometry,
                TargetAngleDeg = Math.Round(targetAngle, 1),
                TargetLengthPx = Math.Round(targetLength, 1),
                NativeAngleDeg = Math.Round(nativeAngle, 1),
                NativeLengthPx = Math.Round(nativeLength, 1),
                RequestedRotationDeg = Math.Round(requestedRotation, 1),
                AppliedRotationDeg = Math.Round(appliedRotation, 1),
                RequestedStretchFactor = Math.Round(requestedStretch, 3),
                AppliedStretchFactor = Math.Round(appliedStretch, 3),
                IsRotationClamped = isRotationClamped,
                IsStretchClamped = isStretchClamped,
                Score = Math.Round(score, 3)
            };
        }

        private static double GetDistance(double x1, double y1, double x2, double y2)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static double GetAngleDegrees(double x1, double y1, double x2, double y2)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            var angle = Math.Atan2(dx, -dy) * (180.0 / Math.PI);
            if (angle < 0.0)
            {
                angle += 360.0;
            }

            return angle;
        }

        private static double NormalizeSignedAngle(double angle)
        {
            while (angle <= -180.0)
            {
                angle += 360.0;
            }

            while (angle > 180.0)
            {
                angle -= 360.0;
            }

            return angle;
        }
    }
}
