using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace ArcticRuins;

public class Test
{
    public static void RunTests()
    {
        var delaunayTestPoints = new Vector2[]
        {
            new(0, 0),
            new(0, 1),
            new(1, 0),
            new(0, -1),
            new(-1, 0),
            new(2, 0),
            new(3, 0),
            new(2, 1),
            new(1.5f, 0.5f),
        };
        var delaunay = new DelaunayHelper(() => delaunayTestPoints);

        AssertDelaunay(new Vector2(0, 0), [
            new Vector2(0, 1),
            new Vector2(0, -1),
            new Vector2(1, 0),
            new Vector2(-1, 0),
        ], delaunay);
        AssertDelaunay(new Vector2(1, 0), [
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(0, -1),
            new Vector2(2, 0),
            new Vector2(1.5f, 0.5f),
        ], delaunay);
        AssertDelaunay(new Vector2(2, 0), [
            new Vector2(0, -1),
            new Vector2(2, 1),
            new Vector2(1, 0),
            new Vector2(3, 0),
            new Vector2(1.5f, 0.5f),
        ], delaunay);

        TestCircle(new Vector2(1, 1), new Vector2(2, 2), new Vector2(1, 2));
        TestCircle(new Vector2(0, 1), new Vector2(10, 20),  new Vector2(-10, 0));
        TestCircle(new Vector2(4, 4), new Vector2(50, 0),  new Vector2(0, 50));
    }

    private static void AssertDelaunay(Vector2 center, HashSet<Vector2> expectedPolygon, DelaunayHelper delaunay)
    {
        var actualPolygon = delaunay.DelaunayPolygonAroundPoint(center);
        if (actualPolygon.Count == expectedPolygon.Count && actualPolygon.All(expectedPolygon.Contains)) return;
        
        Console.WriteLine("Delaunay returned incorrect result for point {0} :(", center);
        Console.WriteLine("Expected:");
        foreach(var v in expectedPolygon)
            Console.WriteLine(v);
        Console.WriteLine("Got:");
        foreach(var v in actualPolygon)
            Console.WriteLine(v);
    }

    private static void TestCircle(Vector2 a, Vector2 b, Vector2 c)
    {
        var circle = DelaunayHelper.GetCircumcircle(a, b, c);
        if (!Mathf.Approximately((a - circle.Center).LengthSquared(), circle.RadiusSqr)
            || !Mathf.Approximately((b - circle.Center).LengthSquared(), circle.RadiusSqr)
            || !Mathf.Approximately((c - circle.Center).LengthSquared(), circle.RadiusSqr))
        {
            Console.WriteLine("Got incorrect circle for points {0} {1} {2} :(", a, b, c);
            Console.WriteLine("Center: {0}, Radius: {1}", circle.Center, circle.Radius);
        }
    }
}