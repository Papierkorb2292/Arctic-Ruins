using System;
using System.Collections.Generic;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;

namespace ArcticRuins;

public class DelaunayHelper(DelaunayHelper.PointProvider points)
{
    private Dictionary<Vector2, List<Vector2>> _surroundingPolygonCache = new();
    private Dictionary<Vector2, List<Circle>> _surroundingCircleCache = new();
    
    /**
     * Returns all neighbors of the given point on the Delaunay triangulation
     * as a polygon in CCW order, starting with the closest point.
     * Only works with points that are not part of the convex hull.
     */
    public List<Vector2> DelaunayPolygonAroundPoint(Vector2 point)
    {
        if (_surroundingPolygonCache.TryGetValue(point, out var polygon))
            return polygon;
        polygon = [];
        // Find closest point first, which is known to be a vertex of the surrounding polygon
        var closestDistSqr = float.MaxValue;
        Vector2? nextVortex = null;
        foreach (var vortex in points())
        {
            if (vortex == point) continue;
            var distanceSqr = Vector2.DistanceSquared(vortex, point);
            if (distanceSqr >= closestDistSqr) continue;
            nextVortex = vortex;
            closestDistSqr = distanceSqr;
        }
        if(nextVortex == null)
            return polygon;

        do
        {
            var prevVortex  = nextVortex.Value;
            polygon.Add(prevVortex);
            nextVortex = null;
            // Calculate next vortex by finding the point that spans the smallest circle on one side of the line.
            // Not a very efficient algorithm, but whatever
            var prevLine = prevVortex - point;
            var prevLineNormal = prevLine.RotateCCW90();
            foreach (var vortex in points())
            {
                if (vortex == point || vortex == prevVortex) continue;
                var product = Vector2.Dot(prevLineNormal, vortex - point);
                if (product <= 0) continue; // Only consider points on the left side of the line (we go counterclockwise)
                if (nextVortex == null || IsPointInCircumcircleCCW(vortex, point, prevVortex, nextVortex.Value))
                {
                    nextVortex = vortex;
                }
            }
        } while (nextVortex != null && nextVortex != polygon[0]);

        _surroundingPolygonCache[point] = polygon;
        return polygon;
    }

    public List<Circle> GetCirclesAroundPoint(Vector2 point)
    {
        if(_surroundingCircleCache.TryGetValue(point, out var circles))
            return circles;
        circles = [];
        var polygon = DelaunayPolygonAroundPoint(point);
        for (int i = 0; i < polygon.Count; i++)
        {
            var v1 = polygon[i];
            var v2 = polygon[(i + 1) % polygon.Count];
            circles.Add(GetCircumcircle(point, v1, v2));
        }
        _surroundingCircleCache[point] = circles;
        return circles;
    }
    
    //TODO(opt): Filter points that are known to be too far away
    public delegate IEnumerable<Vector2> PointProvider();

    public static bool IsPointInCircumcircleCCW(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        // Wikipedia says this works: https://en.wikipedia.org/wiki/Delaunay_triangulation#Algorithms
        var m11 = a.X - point.X;
        var m12 = a.Y - point.Y;
        var m13 = m11 * m11 + m12 * m12;
        var m21 = b.X - point.X;
        var m22 = b.Y - point.Y;
        var m23 = m21 * m21 + m22 * m22;
        var m31 = c.X - point.X;
        var m32 = c.Y - point.Y;
        var m33 =  m31 * m31 + m32 * m32;
        var determinant = m11 * m22 * m33 + m12 * m23 * m31 + m13 * m21 * m32 - m13 * m22 * m31 - m12 * m21 * m33 -
                          m11 * m23 * m32;
        return determinant > 0;
    }

    public static Circle GetCircumcircle(Vector2 a, Vector2 b, Vector2 c)
    {
        var ab = b - a;
        var line1Start = ab / 2 + a;
        var line1Dir = ab.RotateCCW90();
        //Console.WriteLine("Line1: {0} + t{1}", line1Start, line1Dir);
        var ac = c - a;
        var line2Start = ac / 2 + a;
        var line2Dir = ac.RotateCCW90();
        //Console.WriteLine("Line2: {0} + t{1}", line2Start, line2Dir);
        
        // Center lies at intersection of the two lines
        
        var determinant = line1Dir.X * line2Dir.Y - line1Dir.Y * line2Dir.X;
        //Console.WriteLine("determinant: {0}", determinant);
        
        // Multiply first row of inverse matrix by the difference of the start positions
        var t = (line2Dir.Y * (line2Start.X - line1Start.X) - line2Dir.X * (line2Start.Y - line1Start.Y)) / determinant;
        //Console.WriteLine("t: {0}", t);
        var center = line1Start + line1Dir * t;
        return new Circle(center, (center - a).LengthSquared());
    }

    public static float GetDistanceSqrToLineSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var dir = Vector2.Normalize(b - a);
        var projection =  a + dir * Vector2.Dot(point - a, dir);
        // Clamp projection
        var max = Vector2.Max(a, b);
        var min = Vector2.Min(a, b);
        var projClamped = new Vector2(Mathf.Clamp(projection.X, min.X, max.X), Mathf.Clamp(projection.Y, min.Y, max.Y));
        return Vector2.DistanceSquared(projClamped, point);
    }

    public readonly struct Circle(Vector2 center, float radiusSqr, float radius)
    {
        public Circle(Vector2 center, float radiusSqr) : this(center, radiusSqr, Mathf.Sqrt(radiusSqr)) { }
        
        public Vector2 Center => center;
        public float RadiusSqr => radiusSqr;
        public readonly float Radius => radius;

        public bool ContainsPoint(Vector2 point) =>
            (Center - point).LengthSquared() <= RadiusSqr;
        
        public float DistanceToOutside(Vector2 point) =>
            Math.Max(0, (Center - point).Length() - Radius);
    }
}