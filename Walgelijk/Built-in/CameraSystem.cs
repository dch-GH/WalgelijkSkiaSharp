﻿using System;
using System.Linq;
using System.Numerics;

namespace Walgelijk
{
    /// <summary>
    /// System that manages the built in <see cref="CameraComponent"/>
    /// </summary>
    public class CameraSystem : System
    {
        private CameraRenderTask renderTask;
        private ClearRenderTask clearTask;

        /// <summary>
        /// Main camera entity
        /// </summary>
        public Entity MainCameraEntity { get; private set; }

        /// <summary>
        /// Main camera component
        /// </summary>
        public CameraComponent MainCameraComponent { get; private set; }

        /// <summary>
        /// Main camera transform component
        /// </summary>
        public TransformComponent MainCameraTransform { get; private set; }

        /// <summary>
        /// Should the system clear the target as well
        /// </summary>
        public bool DoClear { get; set; } = true;

        private bool mainCameraSet;

        public override void Initialise()
        {
            renderTask = new CameraRenderTask();
            clearTask = new ClearRenderTask();

            if (MainCameraTransform == null)
                FallbackToFirstCamera();
        }

        private void FallbackToFirstCamera()
        {
            var cam = Scene.GetAllComponentsOfType<CameraComponent>();
            if (!cam.Any()) return;
            SetMainCamera(cam.First().Entity);
        }

        /// <summary>
        /// Set the main camera for this system
        /// </summary>
        public void SetMainCamera(Entity cameraEntity)
        {
            CameraComponent camera = GetCameraFrom(cameraEntity);

            MainCameraEntity = cameraEntity;
            MainCameraComponent = camera;
            MainCameraTransform = Scene.GetComponentFrom<TransformComponent>(MainCameraEntity);

            mainCameraSet = true;
        }

        private CameraComponent GetCameraFrom(Entity cameraEntity)
        {
            if (Scene == null)
                throw new InvalidOperationException("System has not been added to a scene yet");

            if (!Scene.HasEntity(cameraEntity))
                throw new ArgumentException($"{cameraEntity} does not exist in the scene");

            if (!Scene.TryGetComponentFrom<CameraComponent>(cameraEntity, out var camera))
                throw new ArgumentException($"{cameraEntity} has no {nameof(CameraComponent)}");

            return camera;
        }

        public override void PreRender()
        {
            if (!mainCameraSet) return;
            SetRenderTask();

            if (DoClear)
                RenderQueue.Add(clearTask, int.MinValue);

            RenderQueue.Add(renderTask, int.MinValue);
        }

        private void SetRenderTask()
        {
            var renderTarget = Scene.Game.Window.RenderTarget;

            renderTask.View = MainCameraTransform.WorldToLocalMatrix;
            var size = renderTarget.Size / MainCameraComponent.PixelsPerUnit * MainCameraComponent.OrthographicSize;
            renderTask.Projection = Matrix4x4.CreateOrthographic(size.X, size.Y, 0, 1);
        }
    }
}
