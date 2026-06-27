using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Coordinates;
using Game.Core.Simulation;
using JetBrains.Annotations;
using Unity.Mathematics;
using Random = UnityEngine.Random;

namespace ArcticRuins;

public class SaveData
{
    public static int CurrentSaveDataVersion = 0;
    
    public readonly Dictionary<GlobalChunkCoordinate, AsteroidData> Asteroids = new();
    public readonly Dictionary<TileDirection, VortexSide> VortexSides = new();
    private readonly Dictionary<string, ShapeSupplierData> VortexShapeSupplier = new();
    public readonly HashSet<GlobalChunkCoordinate> UnremovablePlatforms = [];
    public readonly HashSet<GlobalChunkCoordinate> GeneratedStormChunks = [];
    public Dictionary<GlobalChunkCoordinate, int> DataFragmentChunkLevels = new();
    public readonly TechTracker Tech = new();
    
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

        foreach (var (chunk, level) in rawSaveData.DataFragmentChunkLevels)
        {
            DataFragmentChunkLevels[chunk] = level;
        }
        UnremovablePlatforms = rawSaveData.UnremovablePlatforms;
        GeneratedStormChunks = rawSaveData.GeneratedStormChunks;
        Tech = rawSaveData.Tech;
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

    public class TechTracker
    {
        public List<(List<TechReference> queue, int levelRewardCount)> QueuedRewards = null;
        public HashSet<TechReference> UnlockedRewards = [];
    }

    public class RawSaveData
    {
        public int ArcticRuinsSaveDataVersion = CurrentSaveDataVersion;
        public List<(GlobalChunkCoordinate, AsteroidData)> Asteroids = [];
        public List<(TileDirection, VortexSide)> VortexSides = [];
        public List<(string, ShapeSupplierData)> VortexShapeSupplier = [];
        public HashSet<GlobalChunkCoordinate> UnremovablePlatforms = [];
        public HashSet<GlobalChunkCoordinate> GeneratedStormChunks = [];
        public List<(GlobalChunkCoordinate, int)> DataFragmentChunkLevels = [];
        public TechTracker Tech;
        
        public void CopyFrom(SaveData saveData)
        {
            Asteroids = saveData.Asteroids.Select(entry => (entry.Key, entry.Value)).ToList();
            VortexSides = saveData.VortexSides.Select(entry => (entry.Key, entry.Value)).ToList();
            VortexShapeSupplier = saveData.VortexShapeSupplier.Select(entry => (entry.Key, entry.Value)).ToList();
            UnremovablePlatforms = saveData.UnremovablePlatforms;
            GeneratedStormChunks =  saveData.GeneratedStormChunks;
            DataFragmentChunkLevels = saveData.DataFragmentChunkLevels.Select(entry => (entry.Key, entry.Value)).ToList();
            Tech = saveData.Tech;
        }
    }

    public readonly struct TechReference(int level, int index) : IEquatable<TechReference>
    {
        public int Level => level;
        public int Index => index;

        public override bool Equals(object obj)
        {
            return obj is TechReference other && Equals(other);
        }

        public bool Equals(TechReference other)
        {
            return Level == other.Level && Index == other.Index;
        }

        public override int GetHashCode()
        {
            return 31 * Level.GetHashCode() + Index.GetHashCode();
        }
        
        public static bool operator ==(TechReference left, TechReference right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(TechReference left, TechReference right)
        {
            return !left.Equals(right);
        }
    }
}