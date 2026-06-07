using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Builds a layered render plan for a composite pin on an exact target segment.
    /// </summary>
    public class CompositePinRenderPlanBuilder
    {
        // -------------------------------------------------------------------------
        // Private context records used by the BuildPlan pipeline
        // -------------------------------------------------------------------------

        private sealed record ValidatedInputs(
            PinPartGeometryEntry     Geometry,    // shaft geometry
            PinPartGeometryEntry     HeadEntry,   // head geometry (may differ from Geometry)
            PinPartImageSize         ShaftSize,
            PinPartImageSize         HeadSize,
            PinPartShaftSegmentation Segmentation,
            PinPartPoint             HeadAttach);

        private sealed record PreparedGeometry(
            double        TargetLength,
            double        TargetAngle,
            double        TargetBodyLength,
            double        BodyStretch,
            double        OverallScale,
            Vector        TargetDirection,
            Vector        TargetNormal,
            Point         NativeTip,
            Vector        NativeAxisUnit,
            Vector        NativeNormal,
            Rect          NativeBounds,
            List<Point>   ShaftRectCorners,
            List<Point>   HeadRectCorners);

        private sealed record ComputedTransforms(
            Matrix TipTransform,
            Matrix BodyTransform,
            Matrix HeadCapTransform,
            Matrix HeadTransform,
            double HeadRotationDeg);

        private sealed record ShiftedGeometry(
            Matrix TipTransform,
            Matrix BodyTransform,
            Matrix HeadCapTransform,
            Matrix HeadTransform,
            Point  TipAnchor,
            Point  JoinAnchor,
            Point  StretchStart,
            Point  StretchEnd,
            double CanvasWidth,
            double CanvasHeight);

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        public CompositePinRenderPlan BuildPlan(
            PinPlacementTarget     target,
            PinPartPlacementResult placement,
            PinPartConfig          config,
            PinPartGeometryEntry?  headGeometryOverride = null)
        {
            var validated  = ValidateInputs(target, placement, config, headGeometryOverride);
            var geo        = PrepareGeometry(target, validated);
            var transforms = CalculateTransforms(geo, validated, config);
            var shifted    = CalculateBoundsAndShift(geo, transforms, validated.Segmentation);
            return AssembleResult(placement, config, validated, geo, transforms, shifted);
        }

        // -------------------------------------------------------------------------
        // Pipeline steps
        // -------------------------------------------------------------------------

        private static ValidatedInputs ValidateInputs(
            PinPlacementTarget     target,
            PinPartPlacementResult placement,
            PinPartConfig          config,
            PinPartGeometryEntry?  headGeometryOverride)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (placement == null)
                throw new ArgumentNullException(nameof(placement));
            if (placement.PairGeometry == null)
                throw new ArgumentException("Placement result must include pair geometry.", nameof(placement));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var geometry  = placement.PairGeometry;
            var headEntry = headGeometryOverride ?? geometry;

            var shaftSize = geometry.Shaft.ImageSize
                ?? throw new InvalidOperationException("Shaft image size is required for composite rendering.");
            var headSize = headEntry.Head.ImageSize
                ?? throw new InvalidOperationException("Head image size is required for composite rendering.");
            var segmentation = geometry.Shaft.Segmentation
                ?? throw new InvalidOperationException("Shaft segmentation is required for composite rendering.");
            var headAttach = headEntry.Head.LocalAttach
                ?? throw new InvalidOperationException("Head attach point is required for composite rendering.");

            return new ValidatedInputs(geometry, headEntry, shaftSize, headSize, segmentation, headAttach);
        }

        private static PreparedGeometry PrepareGeometry(
            PinPlacementTarget target,
            ValidatedInputs    v)
        {
            var targetLength    = GetDistance(target.StartScreen, target.EndScreen);
            var targetAngle     = GetAngleDegrees(target.StartScreen, target.EndScreen);
            var targetDirection = Normalize(target.EndScreen - target.StartScreen);
            var targetNormal    = new Vector(-targetDirection.Y, targetDirection.X);

            var nativeTip        = ToPoint(v.Geometry.Shaft.LocalTip);
            var nativeJoin       = ToPoint(v.Geometry.Shaft.LocalJoin);
            var nativeAxis       = nativeJoin - nativeTip;
            var nativeAxisLength = nativeAxis.Length;
            if (nativeAxisLength <= 0.0)
                throw new InvalidOperationException("Shaft native axis length must be greater than zero.");

            var nativeAxisUnit = Normalize(nativeAxis);
            var nativeNormal   = new Vector(-nativeAxisUnit.Y, nativeAxisUnit.X);

            // Overall scale maps source image pixels → screen pixels uniformly.
            // Cap lengths are in source pixels so must be scaled before subtracting from the
            // screen-pixel targetLength — otherwise targetBodyLength is incorrectly negative
            // when the source images are larger than the target screen distance.
            var overallScale     = targetLength / nativeAxisLength;
            var scaledTipCap     = v.Segmentation.TipCapLength  * overallScale;
            var scaledHeadCap    = v.Segmentation.HeadCapLength * overallScale;
            var targetBodyLength = targetLength - scaledTipCap - scaledHeadCap;
            if (targetBodyLength <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Target length {targetLength:F1}px is shorter than the scaled non-stretchable caps " +
                    $"({scaledTipCap + scaledHeadCap:F1}px). " +
                    $"Source caps: tip={v.Segmentation.TipCapLength:F1}px head={v.Segmentation.HeadCapLength:F1}px " +
                    $"native_length={nativeAxisLength:F1}px scale={overallScale:F3}.");
            }

            // bodyStretch is the combined axial scale for the body region
            // (overall scale × any additional stretch to fill targetBodyLength).
            var bodyStretch = targetBodyLength / v.Segmentation.StretchableLength;

            var shaftRectCorners = GetRectangleCorners(v.ShaftSize.Width, v.ShaftSize.Height);
            var headRectCorners  = GetRectangleCorners(v.HeadSize.Width, v.HeadSize.Height);
            var nativeBounds     = new Rect(0, 0, v.ShaftSize.Width, v.ShaftSize.Height);

            return new PreparedGeometry(
                targetLength, targetAngle, targetBodyLength, bodyStretch, overallScale,
                targetDirection, targetNormal,
                nativeTip, nativeAxisUnit, nativeNormal,
                nativeBounds, shaftRectCorners, headRectCorners);
        }

        private static ComputedTransforms CalculateTransforms(
            PreparedGeometry geo,
            ValidatedInputs  v,
            PinPartConfig    config)
        {
            var S   = geo.OverallScale;
            var seg = v.Segmentation;

            // Scaled cap lengths (screen pixels) — the same values computed in PrepareGeometry.
            var scaledTipCap = seg.TipCapLength * S;

            // Local helper deduplicates the five shared axis/normal arguments.
            // normalScale = S maps the source normal direction at the same scale as the axis,
            // preserving the pin image aspect ratio on screen.
            Matrix ShaftLayerTransform(double axialScale, double normalScale, double axialOffset) =>
                CreateLayerTransform(
                    geo.NativeTip, geo.NativeAxisUnit, geo.NativeNormal,
                    geo.TargetDirection, geo.TargetNormal,
                    new Point(0, 0), axialScale, axialOffset, normalScale);

            // Tip cap: uniform scale S (no additional axial stretch).
            var tipTransform = ShaftLayerTransform(S, S, 0.0);

            // Body: axial scale = bodyStretch (= targetBodyLength/StretchableLength, which equals S
            // for a purely uniform scaling and differs from S only when additional stretch is needed).
            // Normal scale = S (always; preserves cross-section width).
            var bodyTransform = ShaftLayerTransform(
                geo.BodyStretch,
                S,
                scaledTipCap - (seg.StretchStartDistance * geo.BodyStretch));

            // Head cap: uniform scale S; placed so StretchEnd aligns with the body end.
            var headCapTransform = ShaftLayerTransform(
                S, S,
                scaledTipCap + geo.TargetBodyLength - seg.StretchEndDistance * S);

            var nativeAttachToCenterAngle = Normalize360(v.HeadEntry.Head.StubDirectionDeg + 180.0);
            var headRotationDeg           = NormalizeSignedAngle(geo.TargetAngle - nativeAttachToCenterAngle);

            // Compute head scale: normalise to TargetHeadRadiusPx so all head images appear
            // at the same screen size.  Fall back to shaft-proportional scale when
            // TargetHeadRadiusPx or LocalRadius is not set (e.g. test data).
            var nativeHeadRadius = v.HeadEntry.Head.LocalRadius;
            var headScale = (config.TargetHeadRadiusPx > 0.0 && nativeHeadRadius > 0.0)
                ? config.TargetHeadRadiusPx / nativeHeadRadius
                : S;

            var headTransform             = CreateHeadTransform(
                v.HeadAttach,
                new Point(geo.TargetDirection.X * geo.TargetLength, geo.TargetDirection.Y * geo.TargetLength),
                headRotationDeg,
                headScale);

            return new ComputedTransforms(tipTransform, bodyTransform, headCapTransform, headTransform, headRotationDeg);
        }

        private static ShiftedGeometry CalculateBoundsAndShift(
            PreparedGeometry         geo,
            ComputedTransforms       t,
            PinPartShaftSegmentation seg)
        {
            var allBounds = new List<Rect>
            {
                GetTransformedBounds(geo.ShaftRectCorners, t.TipTransform),
                GetTransformedBounds(geo.ShaftRectCorners, t.BodyTransform),
                GetTransformedBounds(geo.ShaftRectCorners, t.HeadCapTransform),
                GetTransformedBounds(geo.HeadRectCorners,  t.HeadTransform)
            };

            var unionBounds = Union(allBounds);
            var shiftX = -unionBounds.Left;
            var shiftY = -unionBounds.Top;

            // Matrix is a value type — copying to locals before translating preserves the originals
            var tipT     = t.TipTransform;     tipT.Translate(shiftX, shiftY);
            var bodyT    = t.BodyTransform;    bodyT.Translate(shiftX, shiftY);
            var headCapT = t.HeadCapTransform; headCapT.Translate(shiftX, shiftY);
            var headT    = t.HeadTransform;    headT.Translate(shiftX, shiftY);

            var tipAnchor  = new Point(shiftX, shiftY);
            var joinAnchor = new Point(
                (geo.TargetDirection.X * geo.TargetLength) + shiftX,
                (geo.TargetDirection.Y * geo.TargetLength) + shiftY);
            var scaledTipCap = seg.TipCapLength * geo.OverallScale;
            var stretchStart = new Point(
                tipAnchor.X + (geo.TargetDirection.X * scaledTipCap),
                tipAnchor.Y + (geo.TargetDirection.Y * scaledTipCap));
            var stretchEnd = new Point(
                tipAnchor.X + (geo.TargetDirection.X * (scaledTipCap + geo.TargetBodyLength)),
                tipAnchor.Y + (geo.TargetDirection.Y * (scaledTipCap + geo.TargetBodyLength)));

            return new ShiftedGeometry(
                tipT, bodyT, headCapT, headT,
                tipAnchor, joinAnchor, stretchStart, stretchEnd,
                unionBounds.Width, unionBounds.Height);
        }

        private static CompositePinRenderPlan AssembleResult(
            PinPartPlacementResult placement,
            PinPartConfig          config,
            ValidatedInputs        v,
            PreparedGeometry       geo,
            ComputedTransforms     t,
            ShiftedGeometry        s)
        {
            var geometry   = v.Geometry;
            var seg        = v.Segmentation;
            var shaftFile  = config.UseLitShafts
                ? geometry.ShaftFile.Replace(".png", "_lit.png")
                : geometry.ShaftFile;
            var shaftPath  = Path.Combine(config.PartsFolderPath, shaftFile);
            var headPath   = Path.Combine(config.PartsFolderPath, v.HeadEntry.HeadFile);

            return new CompositePinRenderPlan
            {
                PairId              = placement.PairId,
                ShaftSourcePath     = shaftPath,
                HeadSourcePath      = headPath,
                Width               = Math.Round(s.CanvasWidth,       1),
                Height              = Math.Round(s.CanvasHeight,      1),
                TargetAngleDeg      = Math.Round(geo.TargetAngle,     1),
                TargetLengthPx      = Math.Round(geo.TargetLength,    1),
                HeadRotationDeg     = Math.Round(t.HeadRotationDeg,   1),
                BodyStretchFactor   = Math.Round(geo.BodyStretch,     3),
                StretchBodyLengthPx = Math.Round(geo.TargetBodyLength, 1),
                TipAnchorLocal      = s.TipAnchor,
                JoinAnchorLocal     = s.JoinAnchor,
                StretchStartLocal   = s.StretchStart,
                StretchEndLocal     = s.StretchEnd,
                HeadAttachLocal     = s.JoinAnchor,
                HeadCenterLocal     = TransformPoint(ToPoint(v.HeadEntry.Head.LocalCenter), s.HeadTransform),
                ShaftTipCapLayer = new CompositePinLayerPlan
                {
                    SourcePath   = shaftPath,
                    SourceWidth  = v.ShaftSize.Width,
                    SourceHeight = v.ShaftSize.Height,
                    ClipPolygon  = ClipBand(geo.NativeBounds, geo.NativeTip, geo.NativeAxisUnit,
                                            0.0, seg.TipCapLength),
                    Transform    = s.TipTransform
                },
                ShaftBodyLayer = new CompositePinLayerPlan
                {
                    SourcePath   = shaftPath,
                    SourceWidth  = v.ShaftSize.Width,
                    SourceHeight = v.ShaftSize.Height,
                    ClipPolygon  = ClipBand(geo.NativeBounds, geo.NativeTip, geo.NativeAxisUnit,
                                            seg.StretchStartDistance, seg.StretchEndDistance),
                    Transform    = s.BodyTransform
                },
                ShaftHeadCapLayer = new CompositePinLayerPlan
                {
                    SourcePath   = shaftPath,
                    SourceWidth  = v.ShaftSize.Width,
                    SourceHeight = v.ShaftSize.Height,
                    ClipPolygon  = ClipBand(geo.NativeBounds, geo.NativeTip, geo.NativeAxisUnit,
                                            seg.StretchEndDistance, geometry.Shaft.NativeLength),
                    Transform    = s.HeadCapTransform
                },
                HeadLayer = new CompositePinLayerPlan
                {
                    SourcePath   = headPath,
                    SourceWidth  = v.HeadSize.Width,
                    SourceHeight = v.HeadSize.Height,
                    ClipPolygon  = new List<Point>(geo.HeadRectCorners),
                    Transform    = s.HeadTransform
                }
            };
        }

        // -------------------------------------------------------------------------
        // Low-level geometry helpers
        // -------------------------------------------------------------------------

        private static Point ToPoint(PinPartPoint point)
        {
            return new Point(point.X, point.Y);
        }

        private static double GetDistance(Point start, Point end)
        {
            var delta = end - start;
            return delta.Length;
        }

        private static double GetAngleDegrees(Point start, Point end)
        {
            var delta = end - start;
            var angle = Math.Atan2(delta.X, -delta.Y) * (180.0 / Math.PI);
            return Normalize360(angle);
        }

        private static Vector Normalize(Vector vector)
        {
            vector.Normalize();
            return vector;
        }

        private static Matrix CreateLayerTransform(
            Point  nativeTip,
            Vector nativeAxisUnit,
            Vector nativeNormal,
            Vector targetAxisUnit,
            Vector targetNormal,
            Point  targetTip,
            double axialScale,
            double axialOffset,
            double normalScale = 1.0)
        {
            var m11 = (normalScale * targetNormal.X * nativeNormal.X) + (axialScale * targetAxisUnit.X * nativeAxisUnit.X);
            var m12 = (normalScale * targetNormal.X * nativeNormal.Y) + (axialScale * targetAxisUnit.X * nativeAxisUnit.Y);
            var m21 = (normalScale * targetNormal.Y * nativeNormal.X) + (axialScale * targetAxisUnit.Y * nativeAxisUnit.X);
            var m22 = (normalScale * targetNormal.Y * nativeNormal.Y) + (axialScale * targetAxisUnit.Y * nativeAxisUnit.Y);
            var offsetX = targetTip.X - ((m11 * nativeTip.X) + (m12 * nativeTip.Y)) + (axialOffset * targetAxisUnit.X);
            var offsetY = targetTip.Y - ((m21 * nativeTip.X) + (m22 * nativeTip.Y)) + (axialOffset * targetAxisUnit.Y);

            return new Matrix(m11, m21, m12, m22, offsetX, offsetY);
        }

        private static Matrix CreateHeadTransform(PinPartPoint nativeAttach, Point targetAttach, double rotationDeg, double scale = 1.0)
        {
            var matrix = Matrix.Identity;
            matrix.Scale(scale, scale);
            matrix.Rotate(rotationDeg);
            var scaledRotatedAttach = matrix.Transform(new Point(nativeAttach.X, nativeAttach.Y));
            matrix.Translate(targetAttach.X - scaledRotatedAttach.X, targetAttach.Y - scaledRotatedAttach.Y);
            return matrix;
        }

        private static List<Point> GetRectangleCorners(double width, double height)
        {
            return new List<Point>
            {
                new Point(0, 0),
                new Point(width, 0),
                new Point(width, height),
                new Point(0, height)
            };
        }

        private static Rect GetTransformedBounds(IReadOnlyList<Point> points, Matrix transform)
        {
            var first = TransformPoint(points[0], transform);
            var minX = first.X;
            var maxX = first.X;
            var minY = first.Y;
            var maxY = first.Y;

            for (var i = 1; i < points.Count; i++)
            {
                var point = TransformPoint(points[i], transform);
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }

        private static Point TransformPoint(Point point, Matrix transform)
        {
            return transform.Transform(point);
        }

        private static Rect Union(IReadOnlyList<Rect> rects)
        {
            var result = rects[0];
            for (var i = 1; i < rects.Count; i++)
            {
                result.Union(rects[i]);
            }

            return result;
        }

        private static List<Point> ClipBand(Rect bounds, Point tip, Vector axisUnit, double minDistance, double maxDistance)
        {
            var polygon = new List<Point>
            {
                bounds.TopLeft,
                bounds.TopRight,
                bounds.BottomRight,
                bounds.BottomLeft
            };

            polygon = ClipAgainstHalfPlane(polygon, point => GetAxisDistance(tip, axisUnit, point) >= minDistance, minDistance, tip, axisUnit);
            polygon = ClipAgainstHalfPlane(polygon, point => GetAxisDistance(tip, axisUnit, point) <= maxDistance, maxDistance, tip, axisUnit, keepLessThanOrEqual: true);
            return polygon;
        }

        private static List<Point> ClipAgainstHalfPlane(
            List<Point>     polygon,
            Func<Point, bool> isInside,
            double          boundaryDistance,
            Point           tip,
            Vector          axisUnit,
            bool            keepLessThanOrEqual = false)
        {
            var output = new List<Point>();
            if (polygon.Count == 0)
                return output;

            var previous = polygon[polygon.Count - 1];
            var previousInside = isInside(previous);

            foreach (var current in polygon)
            {
                var currentInside = isInside(current);

                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output.Add(GetBoundaryIntersection(previous, current, boundaryDistance, tip, axisUnit));
                    }

                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(GetBoundaryIntersection(previous, current, boundaryDistance, tip, axisUnit));
                }

                previous = current;
                previousInside = currentInside;
            }

            return output;
        }

        private static Point GetBoundaryIntersection(Point start, Point end, double boundaryDistance, Point tip, Vector axisUnit)
        {
            var startDistance = GetAxisDistance(tip, axisUnit, start);
            var endDistance   = GetAxisDistance(tip, axisUnit, end);
            var delta         = endDistance - startDistance;
            var t             = Math.Abs(delta) < 0.0001 ? 0.0 : (boundaryDistance - startDistance) / delta;

            return new Point(
                start.X + ((end.X - start.X) * t),
                start.Y + ((end.Y - start.Y) * t));
        }

        private static double GetAxisDistance(Point tip, Vector axisUnit, Point point)
        {
            var delta = point - tip;
            return (delta.X * axisUnit.X) + (delta.Y * axisUnit.Y);
        }

        private static double Normalize360(double angle)
        {
            while (angle < 0.0)
            {
                angle += 360.0;
            }

            while (angle >= 360.0)
            {
                angle -= 360.0;
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
