using System.Collections.Generic;
using System.Linq;
using Game.Core.Coordinates;

namespace ArcticRuins;

public class SaveData
{
    public readonly Dictionary<GlobalChunkCoordinate, AsteroidData> Asteroids = new();
    
    public SaveData() {}
    public SaveData(RawSaveData rawSaveData)
    {
        foreach (var (coord, data) in rawSaveData.Asteroids)
        {
            Asteroids[coord] = data;
        }
    }
    
    public class AsteroidData(int totalRequirement, int suppliedShapes)
    {
        public int TotalRequirement = totalRequirement;
        public int SuppliedShapes = suppliedShapes;
        
        public bool IsComplete() => TotalRequirement <= SuppliedShapes;
    }

    public class RawSaveData
    {
        public List<(GlobalChunkCoordinate, AsteroidData)> Asteroids = [];
        
        public void CopyFrom(SaveData saveData)
        {
            Asteroids = saveData.Asteroids.Select(entry => (entry.Key, entry.Value)).ToList();
        }
    }
}