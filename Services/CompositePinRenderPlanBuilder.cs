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
        public CompositePinRenderPlan BuildPlan(
            PinPlacementTarget target,
            PinPartPlacementResult placement,
            PinPartConfig config)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (placement == null)
                throw new ArgumentNullException(nameof(placement));
            if (placement.PairGeometry == null)
                throw new ArgumentException("Placement result must include pair geometry.", nameof(placement));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var geometry = placement.PairGeometry;
            var shaftSize = geometry.Shaft.ImageSize
                ?? throw new InvalidOperationException("Shaft image size is required for composite rendering.");
            var headSize = geometry.Head.ImageSize
                ?? throw new InvalidOperationException("Head image size is required for composite rendering.");
            var segmentation = geometry.Shaft.Segmentation
                ?? throw new InvalidOperationException("Shaft segmentation is required for composite rendering.");
            var headAttach = geometry.Head.LocalAttach
                ?? throw new InvalidOperationException("Head attach point is required for composite rendering.");

            var targetLength = GetDistance(target.StartScreen, target.EndScreen);
            var targetAngle = GetAngleDegrees(target.StartScreen, target.EndScreen);
            var targetBodyLength = targetLength - segmentation.TipCapLength - segmentation.HeadCapLength;
            if (targetBodyLength <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Target length {targetLength:F1}px is shorter than the non-stretchable caps " +
                    $"({segmentation.TipCapLength + segmentation.HeadCapLength:F1}px).");
            }

            var bodyStretch = targetBodyLength / segmentation.StretchableLength;
            var targetDirection = Normalize(target.EndScreen - target.StartScreen);
            var targetNormal = new Vector(-targetDirection.Y, targetDirection.X);

            var nativeTip = ToPoint(geometry.Shaft.LocalTip);
            var nativeJoin = ToPoint(geometry.Shaft.LocalJoin);
            var nativeAxis = nativeJoin - nativeTip;
            var nativeAxisLength = nativeAxis.Length;
            if (nativeAxisLength <= 0.0)
                throw new InvalidOperationException("Shaft native axis length must be greater than zero.");

            var nativeAxisUnit = Normalize(nativeAxis);
            var nativeNormal = new Vector(-nativeAxisUnit.Y, nativeAxisUnit.X);

            var shaftRectCorners = GetRectangleCorners(shaftSize.Width, shaftSize.Height);
            var headRectCorners = GetRectangleCorners(headSize.Width, headSize.Height);
            var nativeBounds = new Rect(0, 0, shaftSize.Width, shaftSize.Height);

            var tipTransform = CreateLayerTransform(
                nativeTip,
                nativeAxisUnit,
                nativeNormal,
                targetDirection,
                targetNormal,
                new Point(0, 0),
                1.0,
                0.0);

            var bodyTransform = CreateLayerTransform(
                nativeTip,
                nativeAxisUnit,
                nativeNormal,
                targetDirection,
                targetNormal,
                new Point(0, 0),
                bodyStretch,
                segmentation.TipCapLength - (segmentation.StretchStartDistance * bodyStretch));

            var headCapTransform = CreateLayerTransform(
                nativeTip,
                nativeAxisUnit,
                nativeNormal,
                targetDirection,
                targetNormal,
                new Point(0, 0),
                1.0,
                segmentation.TipCapLength + targetBodyLength - segmentation.StretchEndDistance);

            var nativeAttachToCenterAngle = Normalize360(geometry.Head.StubDirectionDeg + 180.0);
            var headRotationDeg = NormalizeSignedAngle(targetAngle - nativeAttachToCenterAngle);
            var headTransform = CreateHeadTransform(
                headAttach,
                new Point(targetDirection.X * targetLength, targetDirection.Y * targetLength),
                headRotationDeg);

            var allBounds = new List<Rect>
            {
                GetTransformedBounds(shaftRectCorners, tipTransform),
                GetTransformedBounds(shaftRectCorners, bodyTransform),
                GetTransformedBounds(shaftRectCorners, headCapTransform),
                GetTransformedBounds(headRectCorners, headTransform)
            };

            var unionBounds = Union(allBounds);
            var shiftX = -unionBounds.Left;
            var shiftY = -unionBounds.Top;

            tipTransform.Translate(shiftX, shiftY);
            bodyTransform.Translate(shiftX, shiftY);
            headCapTransform.Translate(shiftX, shiftY);
            headTransform.Translate(shiftX, shiftY);

            var shiftedTargetEnd = new Point((targetDirection.X * targetLength) + shiftX, (targetDirection.Y * targetLength) + shiftY);
            var shiftedTargetStart = new Point(shiftX, shiftY);
            var shiftedStretchStart = new Point(
                shiftedTargetStart.X + (targetDirection.X * segmentation.TipCapLength),
                shiftedTargetStart.Y + (targetDirection.Y * segmentation.TipCapLength));
            var shiftedStretchEnd = new Point(
                shiftedTargetStart.X + (targetDirection.X * (segmentation.TipCapLength + targetBodyLength)),
                shiftedTargetStart.Y + (targetDirection.Y * (segmentation.TipCapLength + targetBodyLength)));

            return new CompositePinRenderPlan
            {
                PairId = placement.PairId,
                ShaftSourcePath = Path.Combine(config.PartsFolderPath, geometry.ShaftFile),
                HeadSourcePath = Path.Combine(config.PartsFolderPath, geometry.HeadFile),
                Width = Math.Round(unionBounds.Width, 1),
                Height = Math.Round(unionBounds.Height, 1),
                TargetAngleDeg = Math.Round(targetAngle, 1),
                TargetLengthPx = Math.Round(targetLength, 1),
                HeadRotationDeg = Math.Round(headRotationDeg, 1),
                BodyStretchFactor = Math.Round(bodyStretch, 3),
                StretchBodyLengthPx = Math.Round(targetBodyLength, 1),
                TipAnchorLocal = shiftedTargetStart,
                JoinAnchorLocal = shiftedTargetEnd,
                StretchStartLocal = shiftedStretchStart,
                StretchEndLocal = shiftedStretchEnd,
                HeadAttachLocal = shiftedTargetEnd,
                HeadCenterLocal = TransformPoint(ToPoint(geometry.Head.LocalCenter), headTransform),
                ShaftTipCapLayer = new CompositePinLayerPlan
                {
                    SourcePath = Path.Combine(config.PartsFolderPath, geometry.ShaftFile),
                    SourceWidth = shaftSize.Width,
                    SourceHeight = shaftSize.Height,
                    ClipPolygon = ClipBand(nativeBounds, nativeTip, nativeAxisUnit, 0.0, segmentation.TipCapLength),
                    Transform = tipTransform
                },
                ShaftBodyLayer = new CompositePinLayerPlan
                {
                    SourcePath = Path.Combine(config.PartsFolderPath, geometry.ShaftFile),
                    SourceWidth = shaftSize.Width,
                    SourceHeight = shaftSize.Height,
                    ClipPolygon = ClipBand(nativeBounds, nativeTip, nativeAxisUnit, segmentation.StretchStartDistance, segmentation.StretchEndDistance),
                    Transform = bodyTransform
                },
                ShaftHeadCapLayer = new CompositePinLayerPlan
                {
                    SourcePath = Path.Combine(config.PartsFolderPath, geometry.ShaftFile),
                    SourceWidth = shaftSize.Width,
                    SourceHeight = shaftSize.Height,
                    ClipPolygon = ClipBand(nativeBounds, nativeTip, nativeAxisUnit, segmentation.StretchEndDistance, geometry.Shaft.NativeLength),
                    Transform = headCapTransform
                },
                HeadLayer = new CompositePinLayerPlan
                {
                    SourcePath = Path.Combine(config.PartsFolderPath, geometry.HeadFile),
                    SourceWidth = headSize.Width,
                    SourceHeight = headSize.Height,
                    ClipPolygon = new List<Point>(headRectCorners),
                    Transform = headTransform
                }
            };
        }

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
            Point nativeTip,
            Vector nativeAxisUnit,
            Vector nativeNormal,
            Vector targetAxisUnit,
            Vector targetNormal,
            Point targetTip,
            double axialScale,
            double axialOffset)
        {
            var m11 = (targetNormal.X * nativeNormal.X) + (axialScale * targetAxisUnit.X * nativeAxisUnit.X);
            var m12 = (targetNormal.X * nativeNormal.Y) + (axialScale * targetAxisUnit.X * nativeAxisUnit.Y);
            var m21 = (targetNormal.Y * nativeNormal.X) + (axialScale * targetAxisUnit.Y * nativeAxisUnit.X);
            var m22 = (targetNormal.Y * nativeNormal.Y) + (axialScale * targetAxisUnit.Y * nativeAxisUnit.Y);
            var offsetX = targetTip.X - ((m11 * nativeTip.X) + (m12 * nativeTip.Y)) + (axialOffset * targetAxisUnit.X);
            var offsetY = targetTip.Y - ((m21 * nativeTip.X) + (m22 * nativeTip.Y)) + (axialOffset * targetAxisUnit.Y);

            return new Matrix(m11, m21, m12, m22, offsetX, offsetY);
        }

        private static Matrix CreateHeadTransform(PinPartPoint nativeAttach, Point targetAttach, double rotationDeg)
        {
            var matrix = Matrix.Identity;
            matrix.Rotate(rotationDeg);
            var rotatedAttach = matrix.Transform(new Point(nativeAttach.X, nativeAttach.Y));
            matrix.Translate(targetAttach.X - rotatedAttach.X, targetAttach.Y - rotatedAttach.Y);
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
            List<Point> polygon,
            Func<Point, bool> isInside,
            double boundaryDistance,
            Point tip,
            Vector axisUnit,
            bool keepLessThanOrEqual = false)
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
            var endDistance = GetAxisDistance(tip, axisUnit, end);
            var delta = endDistance - startDistance;
            var t = Math.Abs(delta) < 0.0001 ? 0.0 : (boundaryDistance - startDistance) / delta;

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
