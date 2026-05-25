using Core.Events;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;
using ShapezShifter.SharpDetour;

namespace ArcticRuins.ShapeAsteroidStabilizer;

using Core.Logging;

// Extend ShapeMiningSystem, because we need to replace it to get notified of islands and stuff, but also the game expects this system to be there
public class ShapeStabilizingSystem : ShapeMiningSystem
{
    public ShapeStabilizingSystem(
        IShapeAsteroidStabilizerConfiguration config,
        BuildingDefinition building,
        IGameResourcesAccessor mapResourceAccessor,
        IShapeRegistry shapes,
        AsteroidProgressSystem asteroidProgressSystem,
        GameMode mode,
        ILogger logger) : base(mapResourceAccessor, shapes, mode, logger)
    {
        this.Set(system => system.ExtractorBuilding, building);
        this.Set(system => system.SimulationCreator, new ShapeAsteroidStabilizerSimulationCreator(config, mapResourceAccessor, asteroidProgressSystem));
    }
    
    private class ShapeAsteroidStabilizerSimulationCreator(IShapeAsteroidStabilizerConfiguration config, IGameResourcesAccessor mapResourceAccessor, AsteroidProgressSystem  asteroidProgressSystem) : IMiningSimulationCreator<ShapeMiningStream>
    {
        public IConnectableSimulation CreateSimulation(
            ShapeMiningStream aggregatedResource,
            BuildingInstance buildingInstance)
        {
            var chunkCoordinate = buildingInstance.Transform.Position.ToChunkCoordinate();
            var originCoordinate = mapResourceAccessor.GetResourceAt_GC(chunkCoordinate).Origin_GC;
            var extractorSimulation = new ShapeAsteroidStabilizerSimulation(
                buildingInstance.State.New<ShapeAsteroidStabilizerSimulationState>(),
                config
                , aggregatedResource, () =>
                {
                    asteroidProgressSystem.OnShapeReceived(originCoordinate);
                });
            return new ConnectableBuildingSimulation(buildingInstance, extractorSimulation);
        }
    }
}
