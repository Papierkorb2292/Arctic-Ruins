using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ArcticRuins.CommunicationRelay;
using Game.Core.Content;
using Game.Core.Rendering.Islands;
using Game.Core.Rendering.Islands.Connectors;
using Game.Core.Rendering.MeshGeneration;
using Global.Core;
using MonoMod.RuntimeDetour;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;
using ShapezShifter.SharpDetour;
using UnityEngine;

namespace ArcticRuins;

public static class MeshRecolorer
{
    private static Hook _savegameHook;
    private static Dictionary<IMeshReference, List<ChangedUV>> _uvChanges = new();
    private static Dictionary<LODMeshMaterialAsset, LODMaterialAsset> _materialChanges = new();
    
    public static void Register()
    {
        _savegameHook = DetourHelper.CreatePostfixHook<GameSessionOrchestrator, IContent, GameData, IGameStartOptions>(
            (orchestrator, content, gameData, options) => orchestrator.Init_3_SavegameAndMode(content, gameData, options),
            (orchestrator, _, _, _) =>
            {
                if (!ArcticRuinsFeatures.GetSelectorForFeature(ArcticRuinsFeatures.MeshRecolorKey).Invoke(orchestrator.Mode.Scenario))
                {
                    try
                    {
                        ResetChanges();
                    }
                    catch (Exception e)
                    {
                        ArcticRuinsMod.Logger.Error!.LogException(e);
                    }
                    return;
                }
                RecolorTheme(orchestrator.Theme);
                RecolorBuildings(orchestrator.Mode.Buildings);
                RecolorIslands(orchestrator.Mode.Islands);
            });
    }

    public static void Dispose()
    {
        _savegameHook.Dispose();
    }
    
    private static void RecolorBuildings(GameBuildings buildings)
    {
        foreach (var group in buildings.All)
        {
            if (group.Id == CommunicationRelayBuilding.GroupId)
                continue; // Communication Relay should still be orange, since it came through the vortex
            foreach (var definition in group.Definitions)
            {
                var drawData = definition.CustomData.Get<BuildingDrawData>();
                foreach (var lodMesh in drawData.MainMeshPerLayer)
                {
                    ChangeLODMeshColor(lodMesh);
                }

                if(definition.TryGetCustomDrawDataAs<IBuildingCustomDrawData>(out var customDrawData))
                    ChangeCustomDrawDataColor(customDrawData);
            }
        }
    }

    private static void RecolorIslands(GameIslands islands)
    {
        // Copy materials from fluid spacer to shape spacer, because the normal shape spacer material always uses the orange accent color
        CopyIslandMaterial(islands.GetDefinition(new IslandDefinitionId("Layout_TrainSpacerStraightShape")), islands.GetDefinition(new IslandDefinitionId("Layout_TrainSpacerStraightFluid")));
        foreach(var definition in islands.AllDefinitions)
        {
            if(definition.CustomData.TryGet<IslandMeshDrawer.Data>(out var drawData))
                ChangeMeshMaterialsColor(drawData.MeshMaterials);
            if(definition.TryCustomDrawDataAs<IIslandCustomDrawData>(out var customDrawData))
                ChangeCustomDrawDataColor(customDrawData);
        }
    }

    private static void RecolorTheme(VisualTheme theme)
    {
        ChangeSpacePathConnectionColor(theme.BaseResources.SpacePathConnectors.BeltInput);
        ChangeSpacePathConnectionColor(theme.BaseResources.SpacePathConnectors.BeltOutput);
        ChangeSpacePathConnectionColor(theme.BaseResources.SpacePathConnectors.PipeInput);
        ChangeSpacePathConnectionColor(theme.BaseResources.SpacePathConnectors.PipeOutput);
        foreach(PathNodeClassification classification in Enum.GetValues(typeof(PathNodeClassification)))
        {
            if (classification == PathNodeClassification.None) continue;
            ChangeMeshMaterialsColor(theme.BaseResources.SpaceBelts.GetMeshMaterials(classification));
        }
        ChangeLODMeshColor(theme.BaseResources.Trains.Cargo.ShapeHatchData.LoadingSafetyLoweringPortForLoading);
        ChangeLODMeshColor(theme.BaseResources.Trains.Cargo.ShapeHatchData.UnloadingSafetyLoweringPortForLoading);
        ChangeLODMeshColor(theme.BaseResources.Trains.Cargo.FluidHatchData.LoadingSafetyLoweringPortForLoading);
        ChangeLODMeshColor(theme.BaseResources.Trains.Cargo.FluidHatchData.UnloadingSafetyLoweringPortForLoading);
    }

    private static void ChangeSpacePathConnectionColor(SpacePathConnectionDrawerMetaData.ConnectionData connection)
    {
        ChangeLODMeshColor(connection.Clip.LODMesh);
        ChangeLODMeshColor(connection.ClipHolo.LODMesh);
        foreach (var asset in connection.Connector)
        {
            ChangeLODMeshColor(asset.LODMesh);
        }
        foreach (var asset in connection.EndCap)
        {
            ChangeLODMeshColor(asset.LODMesh);
        }
    }

    private static void ChangeCustomDrawDataColor(object customDrawData)
    {
        var type = customDrawData.GetType();
        foreach (var allField in BuffExecutor<int>.GetAllFields(type))
        {
            if (allField.FieldType == typeof(LODMeshAsset))
            {
                ChangeLODMeshColor(allField.GetValue(customDrawData) as LODMeshAsset);
            }
            else if(allField.FieldType == typeof(LODMeshAsset[]))
            {
                ChangeLODMeshAssetsColor(allField.GetValue(customDrawData) as LODMeshAsset[]);
            }
            else if (allField.FieldType == typeof(ILODMesh))
            {
                ChangeLODMeshColor(allField.GetValue(customDrawData) as ILODMesh);
            }
        }
        if (customDrawData is BeltPortSenderEntityMetaBuildingDefinition.DrawData receiverData)
        {
            ChangeLODMeshColor(receiverData.Stoppers.StopperLeft);
            ChangeLODMeshColor(receiverData.Stoppers.StopperRight);
        }
    }

    private static void ChangeMeshMaterialsColor(IEnumerable<ILODMeshMaterial> meshMaterials)
    {
        foreach (var mesh in meshMaterials)
        {
            ChangeLODMeshColor(mesh.LODMesh);
        }
    }

    private static void ChangeLODMeshAssetsColor(IEnumerable<LODMeshAsset> assets)
    {
        foreach (var asset in assets)
        {
            ChangeLODMeshColor(asset);
        }
    }
    
    private static void ChangeLODMeshColor(ILODMesh lodMesh)
    {
        for (int lod = 0; lod < lodMesh.Count; lod++)
        {
            if (lodMesh.TryGet(lod, out var mesh))
                ChangeMeshColor(mesh);
        }
    }
    
    private static void ChangeMeshColor(IMeshReference mesh)
    {
        if (_uvChanges.ContainsKey(mesh))
            return; // Already processed
        List<ChangedUV> changes = [];
        var uvs = mesh.GetMeshInternal().uv;
        for (int i = 0; i < uvs.Length; i++)
        {
            // If uv is orange accent, set it to cyan accent
            if (uvs[i] is { x: > 1f / 16 and < 2f / 16, y: > 14f / 16 and < 15f / 16 })
            {
                changes.Add(new ChangedUV(i, uvs[i]));
                uvs[i].y -= 2f / 16;
            }
            // If uv is orange emission, set it to cyan emission
            if (uvs[i] is { x: > 2f / 16 and < 3f / 16, y: > 14f / 16 and < 15f / 16 })
            {
                changes.Add(new ChangedUV(i, uvs[i]));
                uvs[i].y -= 3f / 16;
            }
        }
        mesh.GetMeshInternal().uv = uvs;
        _uvChanges[mesh] = changes;
    }

    private static void CopyIslandMaterial(IIslandDefinition dest, IIslandDefinition src)
    {
        var srcMaterials = src.CustomData.Get<IslandMeshDrawer.Data>().MeshMaterials;
        var destMaterials = dest.CustomData.Get<IslandMeshDrawer.Data>().MeshMaterials;
        var srcAsset = (LODMeshMaterialAsset)srcMaterials[0];
        for (int i = 0; i < destMaterials.Length; i++)
        {
            var destAsset = (LODMeshMaterialAsset)destMaterials[i];
            if(_materialChanges.ContainsKey(destAsset))
                continue;
            _materialChanges[destAsset] = destAsset.MaterialAsset;
            destAsset.MaterialAsset = srcAsset.MaterialAsset;
        }
    }

    private static void ResetChanges()
    {
        foreach (var (mesh, changes) in _uvChanges)
        {
            if (mesh.IsEmpty) continue;
            var uvs = mesh.GetMeshInternal().uv;
            foreach (var change in changes)
            {
                uvs[change.Index] = change.InitialValue;
            }

            mesh.GetMeshInternal().uv = uvs;
        }
        _uvChanges.Clear();
        foreach (var (asset, material) in _materialChanges)
        {
            asset.MaterialAsset = material;
        }
        _materialChanges.Clear();
    }

    private readonly struct ChangedUV(int index, Vector2 initialValue)
    {
        public int Index => index;
        public Vector2 InitialValue => initialValue;
    }
}