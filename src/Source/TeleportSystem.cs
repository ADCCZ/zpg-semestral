using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using semestral;
using System;

namespace semestral
{
    public enum TeleportState
    {
        None,
        FadingOut,
        FadingIn
    }

    public class TeleportSystem : IDisposable
    {
        public TeleportState CurrentState { get; private set; }
        private float fadeAlpha;
        private float fadeTimer;
        private float fadeDuration;
        private Vector3 pendingTargetFeetPosition;
        private Shader fadeShader;
        private int fadeVao;
        private int fadeVbo;
        private bool resourcesInitialized = false;
        private readonly float[] fadeQuadVertices = { -1.0f, -1.0f, 1.0f, -1.0f, -1.0f, 1.0f, -1.0f, 1.0f, 1.0f, -1.0f, 1.0f, 1.0f };

        /// <summary>
        /// Initializes a new instance of the <see cref="TeleportSystem"/> class.
        /// </summary>
        /// <param name="fadeEffectDuration">Duration of the fade in/out effect in seconds.</param>
        /// <param name="fadeVertPath">Path to the vertex shader for the fade effect.</param>
        /// <param name="fadeFragPath">Path to the fragment shader for the fade effect.</param>
        public TeleportSystem(float fadeEffectDuration, string fadeVertPath, string fadeFragPath)
        {
            this.CurrentState = TeleportState.None;
            this.fadeAlpha = 0.0f;
            this.fadeTimer = 0.0f;
            this.fadeDuration = fadeEffectDuration;
            try
            {
                this.fadeShader = new Shader(fadeVertPath, fadeFragPath);
                InitializeRenderObjects();
                resourcesInitialized = true;
                Console.WriteLine("TeleportSystem initialized successfully with shaders.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to initialize TeleportSystem shaders or OpenGL objects: {ex.Message}");
                this.fadeShader = null;
                this.resourcesInitialized = false;
            }
        }

        private void InitializeRenderObjects()
        {
            fadeVao = GL.GenVertexArray();
            fadeVbo = GL.GenBuffer();
            GL.BindVertexArray(fadeVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, fadeVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, fadeQuadVertices.Length * sizeof(float), fadeQuadVertices, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Checks if a teleportation sequence (fade in/out) is currently in progress.
        /// </summary>
        /// <returns>True if teleporting, false otherwise.</returns>
        public bool IsTeleporting()
        {
            return CurrentState != TeleportState.None;
        }

        /// <summary>
        /// Starts the teleportation sequence to the specified target feet position.
        /// </summary>
        /// <param name="targetFeetPos">The world coordinates where the player's feet should be after teleporting.</param>
        public void StartTeleport(Vector3 targetFeetPos)
        {
            if (CurrentState == TeleportState.None && resourcesInitialized)
            {
                this.pendingTargetFeetPosition = targetFeetPos;
                this.CurrentState = TeleportState.FadingOut;
                this.fadeTimer = 0.0f;
                this.fadeAlpha = 0.0f;
                Console.WriteLine($"Teleport initiated: Fading out. Target feet pos: {targetFeetPos}");
            }
            else if (!resourcesInitialized)
            {
                Console.WriteLine("Teleport could not be initiated: TeleportSystem resources not initialized (shader missing?).");
            }
            else
            {
                Console.WriteLine("Teleport could not be initiated: Another teleport is already in progress.");
            }
        }

        /// <summary>
        /// Updates the teleportation fade effect and performs the position change when ready.
        /// Call this every frame from your main update loop.
        /// </summary>
        /// <param name="dt">Delta time for the current frame.</param>
        /// <param name="camera">The player's camera object to be repositioned.</param>
        /// <param name="playerController">The player's controller to reset state after teleport.</param>
        public void Update(float dt, Camera camera, PlayerController playerController)
        {
            if (!resourcesInitialized || camera == null || playerController == null || CurrentState == TeleportState.None) return;

            if (CurrentState == TeleportState.FadingOut)
            {
                fadeTimer += dt;
                fadeAlpha = Math.Min(1.0f, fadeTimer / fadeDuration);
                if (fadeAlpha >= 1.0f)
                {
                    Console.WriteLine("Faded out. Performing actual teleport...");
                    Vector3 newCameraPosition = new Vector3(
                        pendingTargetFeetPosition.X,
                        pendingTargetFeetPosition.Y + Program.PlayerDefaultHeight,
                        pendingTargetFeetPosition.Z
                    );
                    camera.position = newCameraPosition;
                    playerController.NotifyTeleported();
                    Console.WriteLine($"Player teleported to camera pos: X={camera.position.X:F2}, Y={camera.position.Y:F2}, Z={camera.position.Z:F2}");
                    CurrentState = TeleportState.FadingIn;
                    fadeTimer = 0.0f;
                }
            }
            else if (CurrentState == TeleportState.FadingIn)
            {
                fadeTimer += dt;
                fadeAlpha = 1.0f - Math.Min(1.0f, fadeTimer / fadeDuration);
                if (fadeAlpha <= 0.0f)
                {
                    Console.WriteLine("Faded in. Teleport complete.");
                    CurrentState = TeleportState.None;
                    fadeAlpha = 0.0f;
                }
            }
        }

        /// <summary>
        /// Draws the fade overlay (a white screen with varying alpha) if a teleport is in progress.
        /// Call this at the end of your render loop.
        /// </summary>
        public void DrawFadeOverlay()
        {
            if (CurrentState == TeleportState.None || !resourcesInitialized) return;

            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            fadeShader.Use();
            fadeShader.SetUniform("overlayColor", new Vector4(1.0f, 1.0f, 1.0f, fadeAlpha));
            GL.BindVertexArray(fadeVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
        }

        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing) { }
                if (resourcesInitialized)
                {
                    GL.DeleteBuffer(fadeVbo);
                    GL.DeleteVertexArray(fadeVao);
                    fadeShader?.Dispose();
                    Console.WriteLine("TeleportSystem OpenGL resources released.");
                }
                disposedValue = true;
            }
        }
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
    }
}