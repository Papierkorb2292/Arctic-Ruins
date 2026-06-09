using System.Collections.Generic;
using System.Linq;
using Game.Core.Coordinates;
using Game.Core.Rendering;
using Game.Core.Rendering.Culling;
using ShapezShifter.Textures;
using UnityEngine;

namespace ArcticRuins;

public class StormRenderer
{
    private static readonly IMeshReference Mesh = CreateStormMesh();
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");
    private static readonly int ScaleId = Shader.PropertyToID("_Scale");
    private static readonly int TimeScaleId = Shader.PropertyToID("_TimeScale");
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");
    private static readonly int HeightsId = Shader.PropertyToID("_Heights");
    private static readonly MaterialPropertyBlock HeightsBlock = new();
    private const int StormChunkSize = 16;
    private const int StormTileSize = StormChunkSize * CoordinateConstants.TILES_PER_CHUNK;
    private const int StormChunksPerSuperChunk = CoordinateConstants.CHUNKS_PER_SUPER_CHUNK / StormChunkSize;

    private readonly StormLayer[] _detailLayers;
    private readonly StormLayer[] _mapLayers;
    private readonly AsteroidProgressSystem _progressSystem;

    private StormRenderer(GameSessionOrchestrator orchestrator)
    {
        var stormChunks = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/StormChunksMat.mat") };
        var stormNoise = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/StormNoiseMat.mat") };
        var stormBackground = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/StormBackgroundMat.mat") };
        _detailLayers =
        [
            CreateLayer(stormBackground, 0, 1, 1, 0),
            CreateLayer(stormNoise, 0.1f * Mathf.PI, 0.7f, 1, 1),
            CreateLayer(stormChunks, 0.15f * Mathf.PI, 1, 1, 2),
            CreateLayer(stormChunks, -0.1f * Mathf.PI, 1.7f, 0.8f, 3),
            CreateLayer(stormNoise, -0.05f * Mathf.PI, 1, 1, 4),
            CreateLayer(stormChunks, 0.05f * Mathf.PI, 2f, 0.5f, 5),
            CreateLayer(stormChunks, -0.02f * Mathf.PI, 1.5f, 0.7f, 6),
        ];
        _mapLayers =
        [
            CreateLayer(stormBackground, 0, 1, 1, 0),
            CreateLayer(stormNoise, 0, 0.1f, 10, 1),
        ];
        _progressSystem = (AsteroidProgressSystem)orchestrator.SimulationSystems.First(system => system is AsteroidProgressSystem);
    }

    public static void Hook(GameSessionOrchestrator orchestrator)
    {
        if (!ArcticRuinsMod.ArcticRuinsScenarioSelector.Invoke(orchestrator.Mode.Scenario))
            return;
        var stormRenderer = new StormRenderer(orchestrator);
        orchestrator.Draw.Hooks.OnDrawSuperChunk += stormRenderer.Draw;
    }

    private void Draw(FrameDrawOptionsNoLOD options, MapSuperChunk superChunk)
    {
        var layers = options.InOverviewMode ? _mapLayers : _detailLayers;
        for (int x = 0; x < StormChunksPerSuperChunk; x++)
        {
            for (int y = 0; y < StormChunksPerSuperChunk; y++)
            {
                var chunkCoord = superChunk.Origin_GC + new ChunkVector(x * StormChunkSize + StormChunkSize / 2,
                    y * StormChunkSize + StormChunkSize / 2, 1);
                WorldCoordinate pos = chunkCoord.ToOrigin_W() + 50 * WorldVector.Up;
                
                // The height component of each vertex is selected by computing the dot product with the 4d uv (is that still called uv?)
                HeightsBlock.SetVector(HeightsId, new Vector4(
                    CalcStormHeight(chunkCoord + new ChunkVector(StormChunkSize, StormChunkSize, 0)),
                    CalcStormHeight(chunkCoord + new ChunkVector(StormChunkSize, 0, 0)),
                    CalcStormHeight(chunkCoord + new ChunkVector(0, 0, 0)),
                    CalcStormHeight(chunkCoord + new ChunkVector(0, StormChunkSize, 0))
                ));

                for (int i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    options.Renderers.RegularNonInstanced.DrawMesh(layer.Mesh, layer.Material, FastMatrix.TranslateScale(pos + layer.Offset, StormTileSize), RenderCategory.Misc, HeightsBlock);
                }
            }
        }
    }

    private static float CalcStormHeight(GlobalChunkCoordinate cornerPos)
    {
        var dx = cornerPos.x / 100f;
        var dy = cornerPos.y / 100f;
        return -2 + Mathf.Clamp(Mathf.Sqrt(dx * dx + dy * dy), 0, 2);
    }

    private static StormLayer CreateLayer(IMaterialReference material, float angleRad, float scaleMultiplier, float timeScaleMultiplier, int index)
    {
        var copy = material.Copy();
        var copyMat = copy.GetMaterialInternal();
        if (copyMat.HasVector(OffsetId))
        {
            copyMat.SetVector(OffsetId, new Vector2(Random.Range(-20000f, 20000f), Random.Range(-20000f, 200000f)));
        }
        if (copyMat.HasFloat(ScaleId))
        {
            copyMat.SetFloat(ScaleId, copyMat.GetFloat(ScaleId) *  scaleMultiplier);
        }
        if (copyMat.HasFloat(TimeScaleId))
        {
            copyMat.SetFloat(TimeScaleId, copyMat.GetFloat(TimeScaleId) * timeScaleMultiplier);
        }
        if (copyMat.HasFloat(RotationId))
        {
            copyMat.SetFloat(RotationId, angleRad);
        }
        
        return new StormLayer(Mesh, copy, new WorldVector(0, 0, 4 * index));
    }

    private static IMeshReference CreateStormMesh()
    {
        // A plane with larger bounds, because shader changes the height
        var mesh = GeometryHelpers.GeneratePlaneMeshUVUncached();
        var bounds = mesh._Mesh.bounds;
        var extents = bounds.extents;
        extents.y = 64;
        bounds.extents = extents;
        mesh._Mesh.bounds = bounds;
        // Set uv that is used to select the height of the storm
        mesh._Mesh.SetUVs(0, new Vector4[]
        {
            new(1, 0, 0, 0),
            new(0, 1, 0, 0),
            new(0, 0, 1, 0),
            new(0, 0, 0, 1)
        });
        return mesh;
    }

    private class StormLayer(IMeshReference mesh, IMaterialReference material, WorldVector offset)
    {
        public IMeshReference Mesh => mesh;
        public IMaterialReference Material => material;
        public WorldVector Offset => offset;
    }
}