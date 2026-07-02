using System;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Core;
using DG.Tweening.Plugins.Options;
using Game.Core.Coordinates;
using MonoMod.RuntimeDetour;
using ShapezShifter.Kit;
using ShapezShifter.SharpDetour;
using Unity.Mathematics;
using UnityEngine;

namespace ArcticRuins;

public class IntroRenderer
{
    private static Hook _HUDIntroRunHook;
    private static Hook _HUDIntroContinueHook;
    
    private readonly IMaterialReference _vortexMaterial = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/IntroVortexMat.mat") };
    private readonly IMaterialReference _overlayMaterial = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/IntroOverlayMat.mat") };
    private readonly IMaterialReference _titlecardMaterial = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/TitlecardMat.mat") };
    private readonly IMeshReference _vortexMesh = new TemporaryMeshReference(
        FileMeshLoader.LoadSingleMeshFromFile(ArcticRuinsMod.Instance.Resources.SubPath("IntroVortex.fbx"))
    );
    private readonly IMeshReference _rocketMesh = new TemporaryMeshReference(
        FileMeshLoader.LoadSingleMeshFromFile(ArcticRuinsMod.Instance.Resources.SubPath("Rocket.fbx"))
    );
    private readonly IMeshReference _titlecardLeftMesh = new TemporaryMeshReference(
        FileMeshLoader.LoadSingleMeshFromFile(ArcticRuinsMod.Instance.Resources.SubPath("TitlecardLeft.fbx"))
    );
    private readonly IMeshReference _titlecardRightMesh = new TemporaryMeshReference(
        FileMeshLoader.LoadSingleMeshFromFile(ArcticRuinsMod.Instance.Resources.SubPath("TitlecardRight.fbx"))
    );
    
    public HUDCinematicIntro HUDIntro;

    private readonly SoundEffect _vortexExitSound;
    private readonly SoundEffect _titlecardSound;
    private readonly SoundEffect _titlecardBreakSound;
    private readonly SoundEffect _crashSound;
    private readonly SoundEffect _rocketSlowSound;
    private readonly SoundEffect _rocketFastSound;
    
    private ISoundPlayer _soundPlayer;
    private bool _hasStartedVortexAnimation = false;
    
    private Vector3 _camPos = Vector3.zero;
    private Quaternion _camRot = Quaternion.identity;

    private Vector3 _shipPosition = new(0.5f, 0, -1f);

    private static Vector3 _titlecardStartOffset = new(0, 0, 2);
    private static Quaternion _titlecardStartRotation = FastMatrix.RotateXAngle(-30f); 

    private Quaternion _titlecardRotationLeft = _titlecardStartRotation * _titlecardStartRotation; // Rotation is actually doubled, such that it's better visible
    private Vector3 _titlecardPositionLeft = _titlecardStartRotation * _titlecardStartOffset;
    private Quaternion _titlecardRotationRight = _titlecardStartRotation * _titlecardStartRotation;
    private Vector3 _titlecardPositionRight = _titlecardStartRotation * _titlecardStartOffset;

    private float _fadeAlpha = 0;
    private float _fadeScale = 1;
    private bool _finishedVortex = false;

    private Tween _cameraSpinTween;

    private IntroRenderer(GameSessionOrchestrator orchestrator)
    {
        _vortexExitSound = ArcticRuinsMod.Instance.LoadSoundFromAssetBundle("VortexExit.ogg", orchestrator);
        _titlecardSound = ArcticRuinsMod.Instance.LoadSoundFromAssetBundle("Titlecard.ogg", orchestrator);
        _titlecardBreakSound = ArcticRuinsMod.Instance.LoadSoundFromAssetBundle("TitlecardBreak.ogg", orchestrator);
        _crashSound = ArcticRuinsMod.Instance.LoadSoundFromAssetBundle("Crash.ogg", orchestrator);
        _rocketSlowSound = ArcticRuinsMod.Instance.LoadSoundFromAssetBundle("RocketSlow.ogg", orchestrator);
        _rocketFastSound = ArcticRuinsMod.Instance.LoadSoundFromAssetBundle("RocketFast.ogg", orchestrator);
        _soundPlayer = orchestrator._AudioManager.SoundPlayer;
    }

    public static void Register()
    {
        _HUDIntroRunHook = DetourHelper.CreatePostfixHook<HUDCinematicIntro>(
            intro => intro.Run(),
            intro =>
            {
                var introRenderer = ArcticRuinsMod.Instance.IntroRenderer;
                if(introRenderer == null) return;
                introRenderer.HUDIntro = intro;
                introRenderer.OnIntroInit();
            });
        _HUDIntroContinueHook = DetourHelper.CreatePostfixHook<HUDCinematicIntro>(
            intro => intro.OnContinueClicked(),
            _ =>
            {
                ArcticRuinsMod.Instance.IntroRenderer?.OnIntroContinue();
            });
    }

    public static void Dispose()
    {
        _HUDIntroRunHook.Dispose();
        _HUDIntroContinueHook.Dispose();
    }
    
    public static IntroRenderer HookRenderer(GameSessionOrchestrator orchestrator)
    {
        if (!ArcticRuinsMod.ArcticRuinsScenarioSelector.Invoke(orchestrator.Mode.Scenario))
            return null;
        var introRenderer = new IntroRenderer(orchestrator);
        orchestrator.Draw.Hooks.OnDrawSuperChunk += (options, chunk) =>
        {
            introRenderer.Draw(options);
        };
        return introRenderer;
    }

    private void OnIntroInit()
    {
        
        
    }
    
    private void OnIntroContinue()
    {
        var stepSequence = HUDIntro.CameraSequence;
        if (stepSequence == null) return;
        
        if (_hasStartedVortexAnimation)
        {
            _finishedVortex = true;
            stepSequence.Join(DOTween.To(() => _fadeAlpha, alpha => { _fadeAlpha = alpha; }, 0, 1));    
            return;
        }
        _hasStartedVortexAnimation = true;

        var animationSequence = DOTween.Sequence();

        // Title rotates into screen (from above)
        animationSequence.Join(DOTween.To(
                PureQuaternionPlugin.Plug(),
                () => _titlecardStartRotation,
                rot =>
                {
                    _titlecardRotationLeft = _titlecardRotationRight = rot * rot;
                    _titlecardPositionLeft = _titlecardPositionRight = rot * _titlecardStartOffset;
                },
                Quaternion.identity, 5)
            .SetEase(Ease.OutCubic)
            .OnStart(() => _soundPlayer.PlaySound(_titlecardSound)));
        
        // Camera moves to the left, keeps title in view
        animationSequence.Join(DOTween.To(
                () => _camPos,
                pos =>
                {
                    _camPos = pos;
                    _camRot = Quaternion.LookRotation(_titlecardStartOffset - _camPos);
                },
                new Vector3(-0.5f, -0.3f), 2)
            .SetEase(Ease.OutCubic)
            .SetDelay(2)
            .OnComplete(() => _soundPlayer.PlaySound(_rocketSlowSound)));
        
        // Ship flies in and comes to a stop in front of the title
        animationSequence.Join(DOTween.To(
                () => _shipPosition,
                pos => _shipPosition = pos,
                new Vector3(0, 0, 1f), 5)
            .SetEase(Ease.InOutCubic));
        
        // Camera moves back to behind the ship, keeps title in view
        animationSequence.Join(DOTween.To(
                () => _camPos,
                pos =>
                {
                    _camPos = pos;
                    _camRot = Quaternion.LookRotation(_titlecardStartOffset - _camPos);
                },
                new Vector3(-0.4f, 0.3f, -0.05f), 1.5f)
            .SetEase(Ease.InOutCubic)
            .SetDelay(2));
        animationSequence.Join(DOTween.To(
                () => _camPos,
                pos =>
                {
                    _camPos = pos;
                    _camRot = Quaternion.LookRotation(_titlecardStartOffset - _camPos);
                },
                new Vector3(-0.05f, 0.3f, -0.05f), 1.5f)
            .SetEase(Ease.InOutQuad)
            .SetDelay(1.5f));
        
        // Ships accelerate quickly and flies through the title
        animationSequence.Join(DOTween.To(
                () => _shipPosition,
                pos => _shipPosition = pos,
                new Vector3(0f, 0, 6f), 1.1f)
            .SetEase(Ease.Linear)
            .SetDelay(1.5f)
            .OnStart(() => _soundPlayer.PlaySound(_rocketFastSound)));
        // Title breaks apart. First quickly make space for the rocket, then slowly drift apart further
        animationSequence.Join(DOTween.To(
            () => _titlecardPositionLeft + new Vector3(-0.06f, 0, 0),
            pos => _titlecardPositionLeft = pos,
            _titlecardStartOffset + new Vector3(-0.4f, -0.1f, -0.2f), 0.6f)
            .SetEase(Ease.Linear)
            .SetDelay(0.1f)
            .OnStart(() => _soundPlayer.PlaySound(_titlecardBreakSound)));
        animationSequence.Join(DOTween.To(
                () => _titlecardPositionRight + new Vector3(0.06f, 0, 0),
                pos => _titlecardPositionRight = pos,
                _titlecardStartOffset + new Vector3(0.4f, 0.1f, -0.2f), 0.6f)
            .SetEase(Ease.Linear));
        animationSequence.Join(DOTween.To(
                PureQuaternionPlugin.Plug(),
                () => Quaternion.FromToRotation(Vector3.forward, new Vector3(-1, 0, 4).normalized),
                rot => _titlecardRotationLeft = rot,
                Quaternion.FromToRotation(Vector3.forward, new Vector3(-4, -1, 1).normalized), 0.6f)
            .SetEase(Ease.Linear));
        animationSequence.Join(DOTween.To(
            PureQuaternionPlugin.Plug(),
            () => Quaternion.FromToRotation(Vector3.forward, new Vector3(1, 0, 4).normalized),
            rot => _titlecardRotationRight = rot,
            Quaternion.FromToRotation(Vector3.forward, new Vector3(4, 1, 1).normalized), 0.6f)
            .SetEase(Ease.Linear));
        
        // Also move camera forwards
        animationSequence.Join(DOTween.To(
                () => _camPos,
                pos => _camPos = pos,
                new Vector3(0, 0, 5f), 1f)
            .SetEase(Ease.InQuad)
            .SetDelay(0.2f));
        // Aim camera forwards again and rotate around z-axis
        _cameraSpinTween = DOTween.To(
                PureQuaternionPlugin.Plug(),
                () => _camRot,
                rot => _camRot = FastMatrix.RotateZAngle(Angle.FromDegrees(-(_cameraSpinTween.position*_cameraSpinTween.position*_cameraSpinTween.position) * 360)) * rot,
                Quaternion.identity, 2)
            .SetEase(Ease.InCubic)
            .SetDelay(0.2f);
        animationSequence.Join(_cameraSpinTween);
        
        // Fade to white and scale everything in z direction
        animationSequence.Join(DOTween.To(
                () => _fadeScale, 
                alpha => _fadeScale = alpha,
                4, 2)
            .SetEase(Ease.InQuad)
            .OnStart(() => _soundPlayer.PlaySound(_vortexExitSound)));
        animationSequence.Join(DOTween.To(
            () => _fadeAlpha, 
            alpha => _fadeAlpha = alpha,
            1, 1.5f)
            .SetDelay(0.7f));
        
        // Play crash sound with some extra delay at the end
        animationSequence.Join(DOTween.To(
                () => 0,
                _ => { },
                0, 0f)
            .SetDelay(3f)
            .OnStart(() => _soundPlayer.PlaySound(_crashSound)));
        animationSequence.Join(DOTween.To(
            () => 0,
            _ => { },
            0, 0f)
            .SetDelay(5f));
        
        stepSequence.Join(animationSequence.SetDelay(0.5f));
    }

    private void Draw(FrameDrawOptionsNoLOD options)
    {
        if (HUDIntro == null || !HUDIntro.IsActive)
            return; // Only render during the intro
        
        var gameCamPos = options.Viewport.MainCamera.transform.position;
        var gameCamRot = options.Viewport.MainCamera.transform.rotation;

        var scaleFactor = new Vector3(1, 1, _fadeScale);
        var totalRot = gameCamRot * Quaternion.Inverse(_camRot);
        var totalPos = gameCamPos - totalRot * Vector3.Scale(_camPos, scaleFactor);

        var totalMatrix = Matrix4x4.TRS(totalPos, totalRot, scaleFactor);
        //Vector3 pos = new WorldCoordinate(0, 0, 20);
        //var rot = FastMatrix.RotateXAngle(45);
        
        if(!_finishedVortex)
            DrawVortexAnimation(options, totalMatrix);
        
        // Render a quad in front of the camera that is used to fade the image to white at the end of the intro.
        // Also, this quad always writes to the depth buffer, in order to prevent weird outlines on the vortex if post-processing is enabled
        options.Renderers.RegularNonInstanced.DrawMesh(
            GeometryHelpers.BillboardMesh,
            _overlayMaterial,
            Matrix4x4.TRS(
                gameCamPos + gameCamRot * new Vector3(0, 0, 0.5f),
                gameCamRot,
                Vector3.one
            ),
            RenderCategory.Misc,
            MaterialPropertyHelpers.CreateAlphaBlock(_fadeAlpha)
        );
    }

    private void DrawVortexAnimation(FrameDrawOptionsNoLOD options, Matrix4x4 totalMatrix)
    {
        // Render vortex background
        options.Renderers.RegularNonInstanced.DrawMesh(
            _vortexMesh,
            _vortexMaterial,
            totalMatrix * Matrix4x4.Scale(new Vector3(1, 1, 10)),
            RenderCategory.Misc
        );
        // Render both parts of the title
        options.Renderers.RegularNonInstanced.DrawMesh(
            _titlecardLeftMesh,
            _titlecardMaterial,
            totalMatrix * Matrix4x4.TRS(
                _titlecardPositionLeft,
                _titlecardRotationLeft,
                new float3(-0.5f, 0.5f, -0.5f)
            ),
            RenderCategory.Misc
        );
        options.Renderers.RegularNonInstanced.DrawMesh(
            _titlecardRightMesh,
            _titlecardMaterial,
            totalMatrix * Matrix4x4.TRS(
                _titlecardPositionRight,
                _titlecardRotationRight,
                new float3(-0.5f, 0.5f, -0.5f)
            ),
            RenderCategory.Misc
        );
        // Render rocket
        var bouncingShipLocation = _shipPosition + new Vector3(0, Mathf.Sin(options.AnimationSimulationTime_G * 0.5f) * 0.08f, 0);
        options.Renderers.Buildings.Add(
            _rocketMesh,
            options.Theme.BaseResources.BuildingMaterial[0],
            totalMatrix * Matrix4x4.TRS(
                bouncingShipLocation,
                Quaternion.identity,
                new float3(0.06f, 0.06f, 0.06f)
            )
        );   
    }
}