using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ArcticRuins.ShapeAsteroidStabilizer;
using Core.Events;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;
using Game.Core.Map.Simulation.Systems;
using Game.Core.Simulation;
using MonoMod.RuntimeDetour;
using ShapezShifter.Flow;
using ShapezShifter.Hijack;
using ShapezShifter.Kit;
using ShapezShifter.SharpDetour;
using Math = System.Math;

namespace ArcticRuins;

public class AsteroidProgressSystem : IUpdateableSimulationSystem
{
    private static Hook _generateResourcesHook;
    
    //TODO: Idea for uncovering map:
    //1. The starting patches are visible
    //2. If a patch is complete, the three closest patches of all further patches are visible
    //3. A chunk is visible iff the closest patch is visible

    private ConcurrentDictionary<GlobalChunkCoordinate, int> _queuedUpdates = new();

    public void OnShapeReceived(GlobalChunkCoordinate originCoordinate)
    {
        _queuedUpdates[originCoordinate] = _queuedUpdates.GetValueOrDefault(originCoordinate, 0) + 1;
    }
    
    public static void Register()
    {
        _generateResourcesHook = DetourHelper.CreatePostfixHook<MapSuperChunk, IMapGenerator>(
            (chunk, generator) => chunk.GenerateResources(generator),
            (chunk, _) =>
            {
                var saveData = ArcticRuinsMod.Instance.SaveData;
                foreach (var source in chunk.AllResources)
                {
                    if (source is IShapeMapResourceSource)
                    {
                        if(!saveData.Asteroids.ContainsKey(source.Origin_GC))
                            saveData.Asteroids[source.Origin_GC] = new SaveData.AsteroidData(ComputeTotalRequirement(source), 0);
                    }
                    else
                    {
                        //TODO: How to handle fluid patches
                    }
                }
            });
        GameRewirers.AddRewirer<ISimulationSystemsRewirer>(new RegisterAsteroidProgressSystemRewirer());
    }

    private static int ComputeTotalRequirement(IMapResourceSource source)
    {
        // Should increase as you get further away
        return 1000 * Math.Max(Math.Abs(source.CenterOfMass_GC.x), Math.Abs(source.CenterOfMass_GC.y));
    }

    public static void Dispose()
    {
        _generateResourcesHook.Dispose();
    }

    public void Update(Ticks startTicks, Ticks deltaTicks)
    {
        var saveData = ArcticRuinsMod.Instance.SaveData;
        foreach (var coord in _queuedUpdates.Keys)
        {
            if (_queuedUpdates.TryRemove(coord, out var increment) &&
                saveData.Asteroids.TryGetValue(coord, out var asteroid))
            {
                if (asteroid.IsComplete()) return;
                asteroid.SuppliedShapes += increment;
                if (asteroid.IsComplete())
                {
                    //TODO: Unlock map                    
                }
            }
        }
    }

    // Never invoked, because the buildings are already handled by their own simulation system
    private readonly MultiRegisterEvent<IConnectableSimulation> _onSimulationCreated = new();
    private readonly MultiRegisterEvent<IConnectableSimulation> _onBeforeSimulationDestroyed = new();
    public IEvent<IConnectableSimulation> OnSimulationCreated => _onSimulationCreated;
    public IEvent<IConnectableSimulation> OnBeforeSimulationDestroyed => _onBeforeSimulationDestroyed;
    public IEnumerable<IConnectableSimulation> ConnectableSimulations => [];

    private class RegisterAsteroidProgressSystemRewirer : ISimulationSystemsRewirer
    {
        public void ModifySimulationSystems(ICollection<ISimulationSystem> simulationSystems, SimulationSystemsDependencies dependencies)
        {
            if (!ArcticRuinsMod.ArcticRuinsScenarioSelector.Invoke(dependencies.Mode.Scenario))
                return;
            simulationSystems.Add(new AsteroidProgressSystem());
        }

        public bool Equals(IRewirer other) => other is RegisterAsteroidProgressSystemRewirer;
    }
}