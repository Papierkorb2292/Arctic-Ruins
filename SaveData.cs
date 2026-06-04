using System.Collections.Generic;
using System.Linq;
using Game.Core.Coordinates;
using Game.Core.Simulation;
using JetBrains.Annotations;

namespace ArcticRuins;

public class SaveData
{
    public readonly Dictionary<GlobalChunkCoordinate, AsteroidData> Asteroids = new();
    public readonly Dictionary<TileDirection, VortexSide> VortexSides = new();
    private readonly Dictionary<string, ShapeSupplierData> VortexShapeSupplier = new();
    
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

        foreach (var (shape, data) in rawSaveData.VortexShapeSupplier)
        {
            VortexShapeSupplier[shape] = data;
        }
    }

    [CanBeNull]
    public ResearchCostShapes GetShapeForVortexSide(TileDirection dir) => VortexSides.GetValueOrDefault(dir)?.Shape;

    public void SetShapeForVortexSide(TileDirection dir, ResearchCostShapes shape)
    {
        if (VortexSides.TryGetValue(dir, out var side))
            side.Shape = shape;
        else
            VortexSides[dir] = new VortexSide
            {
                Shape = shape
            };
    }

    public ShapeSupplierData GetVortexShapeSupplierData(string hash)
    {    
        if (VortexShapeSupplier.TryGetValue(hash, out var data))
            return data;
        return VortexShapeSupplier[hash] = new ShapeSupplierData();
    }

    public class AsteroidData(int totalRequirement, int suppliedShapes)
    {
        public int TotalRequirement = totalRequirement;
        public int SuppliedShapes = suppliedShapes;
        
        public bool IsComplete() => TotalRequirement <= SuppliedShapes;
    }
    
    public class ShapeSupplierData
    {
        public int roundRobinLocation { get; set; } = 0;
        public Steps progressSteps { get; set; } = Steps.Zero;
    }

    public class VortexSide
    {
        public ResearchCostShapes Shape { get; set; }
    }

    public class RawSaveData
    {
        public List<(GlobalChunkCoordinate, AsteroidData)> Asteroids = [];
        public List<(TileDirection, VortexSide)> VortexSides = [];
        public List<(string, ShapeSupplierData)> VortexShapeSupplier = [];
        
        public void CopyFrom(SaveData saveData)
        {
            Asteroids = saveData.Asteroids.Select(entry => (entry.Key, entry.Value)).ToList();
            VortexSides = saveData.VortexSides.Select(entry => (entry.Key, entry.Value)).ToList();
            VortexShapeSupplier = saveData.VortexShapeSupplier.Select(entry => (entry.Key, entry.Value)).ToList();
        }
    }
}