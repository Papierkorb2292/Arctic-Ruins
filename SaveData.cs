using System.Collections.Generic;
using System.Linq;
using Game.Core.Coordinates;
using JetBrains.Annotations;

namespace ArcticRuins;

public class SaveData
{
    public readonly Dictionary<GlobalChunkCoordinate, AsteroidData> Asteroids = new();
    public readonly Dictionary<TileDirection, VortexSide> VortexSides = new();
    
    public SaveData() {}
    public SaveData(RawSaveData rawSaveData)
    {
        foreach (var (coord, data) in rawSaveData.Asteroids)
        {
            Asteroids[coord] = data;
        }

        foreach (var (dir, side) in rawSaveData.VortexSides)
        {
            VortexSides[dir] = side;
        }
    }

    [CanBeNull]
    public string GetShapeForVortexSide(TileDirection dir) => VortexSides.GetValueOrDefault(dir)?.ConfiguredShapeHash;

    public void SetShapeForVortexSide(TileDirection dir, string shape)
    {
        if (VortexSides.TryGetValue(dir, out var side))
        {
            side.ConfiguredShapeHash = shape;
        }
        else
        {
            VortexSides[dir] = new VortexSide
            {
                ConfiguredShapeHash = shape
            };
        }
    }
    
    public class AsteroidData(int totalRequirement, int suppliedShapes)
    {
        public int TotalRequirement = totalRequirement;
        public int SuppliedShapes = suppliedShapes;
        
        public bool IsComplete() => TotalRequirement <= SuppliedShapes;
    }

    public class VortexSide
    {
        public string ConfiguredShapeHash { get; set; }
    }

    public class RawSaveData
    {
        public List<(GlobalChunkCoordinate, AsteroidData)> Asteroids = [];
        public List<(TileDirection, VortexSide)> VortexSides = [];
        
        public void CopyFrom(SaveData saveData)
        {
            Asteroids = saveData.Asteroids.Select(entry => (entry.Key, entry.Value)).ToList();
            VortexSides = saveData.VortexSides.Select(entry => (entry.Key, entry.Value)).ToList();
        }
    }
}