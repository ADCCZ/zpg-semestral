using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System; // Pro Math, Exception
using System.Collections.Generic; // Pro Queue
using System.IO; // Pro Path
using semestral; 

public class Program : GameWindow
{
    public static void Main() => new Program().Run();

    Viewport mainViewport;
    Camera mainCamera;
    Light flashlight;
    World world;
    Viewport minimapViewport;
    Minimap minimap;
    PlayerController playerController;
    TeleportSystem teleportSystem;

    public const float PlayerDefaultHeight = 1.7f;
    public const float PlayerDefaultCollisionRadius = 0.3f;
    public const float PlayerDefaultGravity = -9.81f;
    public const float PlayerDefaultJumpForce = 6.0f;
    public const float PlayerFlashlightYOffset = 0.35f;

    public const float weight = 80.0f;

    public const float WorldCellWidth = 2.0f;
    public const float WorldCellDepth = 2.0f;
    public const float WorldStoryHeight = 3.0f;
    public const float WorldSlabHeight = 0.1f;

    const float depressionAngle = (float)(-2 * Math.PI / 180 / 180 * Math.PI); // Opraveno na radiány
    Vector2 mouseDelta;

    private double totalFrameTime;
    private Queue<double> frameTimeQueue = new Queue<double>();
    private string shaderPath = "Shaders/";
    private const float TeleportEffectDuration = 0.5f;

    public Program() : base(
        new GameWindowSettings { /* UpdateFrequency = 60.0 */ },
        new NativeWindowSettings { ClientSize = new Vector2i(1920, 1080), Title = "Semestrální práce ZPG" })
    { }

    /// <summary>
    /// Called when the game window is loaded. Sets up initial game state and resources.
    /// </summary>
    protected override void OnLoad()
    {
        base.OnLoad();
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
        GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);
        GL.Enable(EnableCap.DepthTest);
        GL.ClearColor(0.1f, 0.1f, 0.15f, 1.0f); // Tmavší pozadí
        CursorState = CursorState.Grabbed;

        mainViewport = new Viewport() { Left = 0, Top = 0, Width = 1, Height = 1, window = this };

        try
        {
            world = new World("Assets/mapa4.txt"); 
            Console.WriteLine("World object created successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize World: {ex.Message}");
        }

        minimapViewport = new Viewport { Left = 0.01, Top = 0.01, Width = 0.25, Height = 0.25, window = this }; // Menší minimapa
        minimap = new Minimap(Path.Combine(shaderPath, "minimap.vert"), Path.Combine(shaderPath, "minimap.frag"));

        InitializePlayerAndController();

        Console.WriteLine($"DEBUG: Initializing TeleportSystem with shaderPath: '{this.shaderPath}'");
        string fullVertPath = Path.Combine(this.shaderPath, "fade.vert");
        string fullFragPath = Path.Combine(this.shaderPath, "fade.frag");
        Console.WriteLine($"DEBUG: Full vert path: '{Path.GetFullPath(fullVertPath)}', Exists: {File.Exists(fullVertPath)}");
        Console.WriteLine($"DEBUG: Full frag path: '{Path.GetFullPath(fullFragPath)}', Exists: {File.Exists(fullFragPath)}");


        if (!string.IsNullOrEmpty(this.shaderPath))
        {
            teleportSystem = new TeleportSystem(TeleportEffectDuration,
                                               Path.Combine(this.shaderPath, "fade.vert"),
                                               Path.Combine(this.shaderPath, "fade.frag"));
            Console.WriteLine("TeleportSystem initialized.");
        }
        else
        {
            Console.WriteLine("ERROR: shaderPath is not set, cannot initialize TeleportSystem shaders.");
        }
    }

    /// <summary>
    /// Initializes the player camera, flashlight, and player controller.
    /// Should be called after the world is loaded.
    /// </summary>
    private void InitializePlayerAndController()
    {
        mainCamera = new Camera(mainViewport);

        if (world == null)
        {
            Console.WriteLine("CRITICAL ERROR: World object is not initialized. Cannot fully initialize player. Check map loading in OnLoad().");
            mainCamera.position = new Vector3(0f, PlayerDefaultHeight, 0f);
            if (flashlight == null)
            { // Inicializace baterky i v případě chyby světa, pokud je to žádoucí
                flashlight = new Light(
                   new Vector3(mainCamera.position.X, mainCamera.position.Y + PlayerFlashlightYOffset, mainCamera.position.Z), true);
            }
            Console.WriteLine("PlayerController could not be initialized because the world object is null.");
            return;
        }

        Vector3 initialCameraPos;
        if (world.StartPositionLevel != -1)
        {
            initialCameraPos = new Vector3(
                world.StartPos.X,
                world.StartPos.Y + PlayerDefaultHeight,
                world.StartPos.Z
            );
            Console.WriteLine($"Player starting at: {initialCameraPos} (Level: {world.StartPositionLevel})");
        }
        else
        {
            initialCameraPos = new Vector3(0f, PlayerDefaultHeight, 0f);
            Console.WriteLine("Warning: Start position ('@') not found in map. Player starting at default position.");
        }
        mainCamera.position = initialCameraPos;

        flashlight = new Light(
            new Vector3(mainCamera.position.X, mainCamera.position.Y + PlayerFlashlightYOffset, mainCamera.position.Z),
            true
        );

        playerController = new PlayerController(mainCamera, flashlight, world,
                                                PlayerDefaultHeight,
                                                PlayerDefaultCollisionRadius,
                                                PlayerDefaultGravity,
                                                PlayerFlashlightYOffset,
                                                WorldStoryHeight);
        Console.WriteLine("Player and PlayerController initialized.");
    }

    /// <summary>
    /// Initiates the player teleport sequence to the specified target feet position.
    /// </summary>
    /// <param name="targetFeetPositionAtDestination">The world coordinates where the player's feet should be after teleporting.</param>
    public void InitiatePlayerTeleport(Vector3 targetFeetPositionAtDestination)
    {
        if (teleportSystem != null)
        {
            Console.WriteLine($"Initiating teleport to {targetFeetPositionAtDestination}");
            teleportSystem.StartTeleport(targetFeetPositionAtDestination);
        }
        else
        {
            Console.WriteLine("Cannot initiate teleport: TeleportSystem is not initialized.");
        }
    }

    /// <summary>
    /// Called every frame to update game logic and state.
    /// </summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        float dt = (float)args.Time;

        if (KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Escape))
        {
            Close();
        }

        teleportSystem?.Update(dt, mainCamera, playerController);

        if (teleportSystem == null || !teleportSystem.IsTeleporting())
        {
            ProcessPlayerInputAndMovement(dt);
            playerController?.UpdateVerticalState(dt);

            if (playerController != null && playerController.IsGrounded &&
                world != null && world.Teleporters != null && mainCamera != null)
            {
                float playerFeetY = mainCamera.position.Y - PlayerDefaultHeight;
                Vector3 playerFeetPositionForActivation = new Vector3(mainCamera.position.X, playerFeetY, mainCamera.position.Z);

                // Procházíme instance Teleporter objektů definovaných ve světě
                foreach (var teleporterInstance in world.Teleporters)
                {
                    teleporterInstance.UpdateCooldownAndExitState(dt, playerFeetPositionForActivation);

                    if (teleporterInstance.TryActivate(playerFeetPositionForActivation))
                    {
                        Console.WriteLine($"Player activated teleporter (Map: {teleporterInstance.MapCoordinates}, World: {teleporterInstance.WorldPadPosition}) targeting-> {teleporterInstance.TargetFeetPosition}");
                        InitiatePlayerTeleport(teleporterInstance.TargetFeetPosition);

                        teleporterInstance.MarkAsUsedAndStartCooldown();
                        teleporterInstance.Partner?.MarkAsUsedAndStartCooldown();
                        break; 
                    }
                }
            }
        }

        ProcessMouseLook(dt);
        world?.UpdateAllModels(dt);
    }

    /// <summary>
    /// Processes keyboard input for player movement and actions like jumping.
    /// </summary>
    private void ProcessPlayerInputAndMovement(float dt)
    {
        if (playerController == null || mainCamera == null || world == null) return;

        Vector3 moveDir = Vector3.Zero;
        Vector3 cameraForwardXZ = new Vector3(mainCamera.GetDirection().X, 0, mainCamera.GetDirection().Z).Normalized();
        Vector3 cameraRightXZ = Vector3.Cross(cameraForwardXZ, Vector3.UnitY).Normalized();

        if (KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.W)) moveDir += cameraForwardXZ;
        if (KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.S)) moveDir -= cameraForwardXZ;
        if (KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.A)) moveDir -= cameraRightXZ;
        if (KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D)) moveDir += cameraRightXZ;

        if (KeyboardState.IsKeyPressed(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Space))
        {
            playerController.Jump(Program.PlayerDefaultJumpForce);
        }

        if (moveDir.LengthSquared > 0.0001f)
        {
            mainCamera.TryMove(moveDir.Normalized(), dt, world.worldModels);
        }
    }

    /// <summary>
    /// Processes mouse input for camera look.
    /// </summary>
    private void ProcessMouseLook(float dt)
    {
        if (mainCamera == null || !IsFocused) return;

        if (mouseDelta.LengthSquared > 0.00001f)
        {
            mainCamera.RotateX(mouseDelta.Y * dt);
            mainCamera.RotateY(mouseDelta.X * dt);

            if (flashlight != null)
            {
                Quaternion depression = Quaternion.FromAxisAngle(
                    Vector3.Normalize(Vector3.Cross(mainCamera.GetDirection(), Vector3.UnitY)),
                    depressionAngle
                );
                flashlight.direction = Vector3.Normalize(Vector3.Transform(mainCamera.GetDirection(), depression));
            }
        }
        mouseDelta = Vector2.Zero;
    }

    /// <summary>
    /// Called every frame to render the scene.
    /// </summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        GL.ClearColor(0.1f, 0.1f, 0.15f, 1.0f);
        mainViewport.Set();
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        world?.RenderAllModels(mainCamera, flashlight);
        minimap?.Render(world, mainCamera, minimapViewport,
                       Program.PlayerDefaultHeight,
                       Program.WorldStoryHeight,
                       Program.WorldCellWidth,
                       Program.WorldCellDepth);

        if (teleportSystem != null && teleportSystem.IsTeleporting()) 
        {
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y); 
            teleportSystem.DrawFadeOverlay();
        }

        updateFPS(args);
        SwapBuffers();
    }

    private static void DebugCallback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
    {
        string msg = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message, length);
        Console.WriteLine($"OpenGL Debug Message: {msg} (Source: {source}, Type: {type}, Severity: {severity}, ID: {id})");
    }

    /// <summary>
    /// Showing the current FPS in the window title.
    /// </summary>
    private void updateFPS(FrameEventArgs args)
    {
        totalFrameTime += args.Time;
        frameTimeQueue.Enqueue(totalFrameTime);
        while (frameTimeQueue.Count > 0 && frameTimeQueue.Peek() < totalFrameTime - 1.0)
        {
            frameTimeQueue.Dequeue();
        }
        int fps = frameTimeQueue.Count;
        Title = $"Semestral ZPG - FPS: {fps}";
    }

    /// <summary>
    /// Called when the game window is being resized.
    /// </summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        if (mainViewport != null) mainViewport.Set(); 
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y); 
    }

    /// <summary>
    /// Called when the mouse moves.
    /// </summary>
    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsFocused) // Only accumulate mouse delta if the window is focused
        {
            mouseDelta += e.Delta;
        }
    }
}