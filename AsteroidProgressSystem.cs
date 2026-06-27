using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ArcticRuins.ShapeAsteroidStabilizer;
using Core.Collections.Scoped;
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
using Unity.Mathematics;
using UnityEngine;
using Math = System.Math;

namespace ArcticRuins;

public class AsteroidProgressSystem : IUpdateableSimulationSystem
{
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int AspectRatioId = Shader.PropertyToID("_AspectRatio");
    private static readonly MaterialPropertyBlock MaterialPropertyBlock = new();
    private static Hook _generateResourcesHook;

    private ConcurrentDictionary<GlobalChunkCoordinate, int> _queuedUpdates = new();
    
    public MultiRegisterEvent<GlobalChunkCoordinate, SaveData.AsteroidData> OnAsteroidProgressUpdate = new();

    public void OnShapeReceived(GlobalChunkCoordinate originCoordinate)
    {
        _queuedUpdates.AddOrUpdate(originCoordinate, 1, (_, num) => num + 1);
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

    public static void HookRenderer(GameSessionOrchestrator orchestrator)
    {
        var system = (AsteroidProgressSystem)orchestrator.SimulationSystems.FirstOrDefault(system => system is AsteroidProgressSystem);
        if (system != null)
        {
            var shapeVisualization = ((HUDVisualizations)orchestrator.HUD.Parts.First(part =>
                    part is HUDVisualizations)).Visualizations
                .First(visualization => visualization.Visualization is HUDShapeResourcesVisualization).Visualization;
            var coordinateVisualization = (HUDSuperChunkCoordinatesVisualization)((HUDVisualizations)orchestrator.HUD.Parts.First(part =>
                    part is HUDVisualizations)).Visualizations
                .First(visualization => visualization.Visualization is HUDSuperChunkCoordinatesVisualization).Visualization;
            var progressBar = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/ProgressBarMat.mat") };
            orchestrator.Draw.Hooks.OnDrawSuperChunk += (options, chunk) => DrawSuperChunk(options, chunk, shapeVisualization, system, progressBar, coordinateVisualization);
        }
    }

    // Register the system on demand, because rewirer invoke order is arbitrary
    public static AsteroidProgressSystem GetOrRegisterSimulationSystem(ICollection<ISimulationSystem> simulationSystems)
    {
        var system = simulationSystems.OfType<AsteroidProgressSystem>().FirstOrDefault();
        if (system != null) return system;
        system = new AsteroidProgressSystem();
        simulationSystems.Add(system);
        return system;
    }

    private static int ComputeTotalRequirement(IMapResourceSource source)
    {
        // Special case for the chunks at the center, since they'd get very small numbers otherwise, and wouldn't
        // be consistent with each other
        if (source.CenterOfMass_GC.ManhattanDistance(GlobalChunkCoordinate.Origin) < 5)
            return 100;
        // Should increase as you get further away
        return RoundToTwoSignificantDigits(20 * Math.Max(Math.Abs(source.CenterOfMass_GC.x), Math.Abs(source.CenterOfMass_GC.y)));
    }

    private static int RoundToTwoSignificantDigits(int number)
    {
        var magnitude = (int)Math.Log10(number);
        if (magnitude <= 1)
            return number;
        var scale = (int)Math.Pow(10, magnitude - 1);
        return (number / scale) * scale;
    }

    public static void Dispose()
    {
        _generateResourcesHook.Dispose();
    }

    public void Update(Ticks startTicks, Ticks deltaTicks)
    {
        var saveData = ArcticRuinsMod.Instance.SaveData;
        using var queued = ScopedList.Get(_queuedUpdates.Keys);
        foreach (var coord in queued)
        {
            if (!_queuedUpdates.TryRemove(coord, out var increment) || !saveData.Asteroids.TryGetValue(coord, out var asteroid) || asteroid.IsComplete()) continue;
            asteroid.SuppliedShapes = Math.Min(asteroid.SuppliedShapes + increment, asteroid.TotalRequirement);
            OnAsteroidProgressUpdate.Invoke(coord, asteroid);
        }
    }

    private static void DrawSuperChunk(FrameDrawOptionsNoLOD options, MapSuperChunk chunk,
        HUDVisualization shapeVisualization, AsteroidProgressSystem system, IMaterialReference progressBar,
        HUDSuperChunkCoordinatesVisualization coordinatesVisualization)
    {
        // Like in HUDShapeResourcesVisualization
        float alpha = shapeVisualization?.Alpha ?? 1;
        if (alpha < 1.0 / 1000.0)
            return;
        float scale = (8.5f + math.min(options.Viewport.Zoom, 19000f) * 0.005f) * alpha;
        if (scale < 1.0 / 1000.0)
            return;
        var mapResourceSourceList = chunk.ResourcesOfType<IShapeMapResourceSource>();
        int count = mapResourceSourceList.Count;
        for (int index = 0; index < count; ++index)
        {
            var mapResourceSource = mapResourceSourceList[index];
            if (!ArcticRuinsMod.Instance.SaveData.Asteroids.TryGetValue(mapResourceSource.Origin_GC, out var asteroidData))
                continue;
            var totalRequirement = asteroidData.TotalRequirement.ToString();
            var suppliedShapes = asteroidData.SuppliedShapes.ToString();
            var text = new string(' ', Math.Max(0, totalRequirement.Length - suppliedShapes.Length)) + suppliedShapes + '/' + totalRequirement;
            var aspectRatio = text.Length;
            var progress = (float)asteroidData.SuppliedShapes / asteroidData.TotalRequirement;
            MaterialPropertyBlock.SetFloat(ProgressId, progress);
            MaterialPropertyBlock.SetFloat(AspectRatioId, aspectRatio);
            var pos_W = mapResourceSource.CenterOfMass_GC.ToCenter_W() + new WorldVector(0.0f, 2.6f * scale, 4f);
            options.Renderers.UINonInstanced.DrawMesh(GeometryHelpers.PlaneMesh, progressBar,
                FastMatrix.TranslateScale(in pos_W, new float3(scale * aspectRatio, 0.01f, scale)),
                RenderCategory.ShapeMapResources, MaterialPropertyBlock);
            if (!options.InOverviewMode)
                RenderProgressText(options, text, options.Theme.BaseResources.UXSuperChunkCoordinatesRendererMaterial, pos_W, scale * 0.9f, coordinatesVisualization);
        }
    }

    private static readonly float Sqrt2 = Mathf.Sqrt(2);

    private static void RenderProgressText(FrameDrawOptionsNoLOD options, string text, IMaterialReference textMaterial,
        WorldCoordinate pos_W, float scale, HUDSuperChunkCoordinatesVisualization coordinatesVisualization)
    {
        pos_W += WorldVector.West * (text.Length - 1) / 2f * scale;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            int meshIndex = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'A' and <= 'Z' => c - 'A' + 10,
                >= 'a' and <= 'z' => c - 'a' + 10,
                '/' => HUDSuperChunkCoordinatesVisualization.LetterIndexSlash,
                _ => -1
            };
            if (meshIndex != -1)
            {
                var matrix = FastMatrix.TranslateScale(in pos_W, scale);
                if (c == '/')
                    matrix.m20 = -(matrix.m02 = matrix.m22 = matrix.m00 = scale / Sqrt2); // Rotate by 45°
                options.Renderers.UI.Add(coordinatesVisualization.PlaneMeshes[meshIndex], textMaterial, matrix);
            }

            pos_W += WorldVector.East * scale;
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
            GetOrRegisterSimulationSystem(simulationSystems);
        }

        public bool Equals(IRewirer other) => other is RegisterAsteroidProgressSystemRewirer;
    }
}