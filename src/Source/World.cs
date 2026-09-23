using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenTK.Mathematics;
using semestral; // Assuming this namespace contains WorldElement, Model, Light, Teleporter, Material, AABB, Camera etc.

/// <summary>
/// Represents the game world, including its structure, visual models, lights, and interactive elements.
/// It loads map data from a file and generates the corresponding 3D environment.
/// </summary>
public class World
{
    /// <summary>
    /// Gets the list of 3D models that make up the visual representation of the world.
    /// </summary>
    public List<Model> worldModels { get; private set; }

    /// <summary>
    /// Gets the list of light sources present in the world.
    /// </summary>
    public List<Light> worldLights { get; private set; }

    /// <summary>
    /// Gets the player's starting position in world coordinates (X, Y, Z).
    /// </summary>
    public Vector3 StartPos { get; private set; }

    /// <summary>
    /// Gets the level (floor index) of the player's starting position. Defaults to -1 if not set.
    /// </summary>
    public int StartPositionLevel { get; private set; } = -1;

    /// <summary>
    /// Gets the list of teleporter instances in the world.
    /// </summary>
    public List<Teleporter> Teleporters { get; private set; }

    /// <summary>
    /// Raw 3D array representing the map data. Stores <see cref="WorldElement"/> types for each cell.
    /// Dimensions are [level, row, column].
    /// </summary>
    public WorldElement[,,] worldData;

    /// <summary>
    /// The width of the map in grid cells.
    /// </summary>
    public int width;

    /// <summary>
    /// The height (or depth along Z-axis in map file) of the map in grid cells per level.
    /// </summary>
    public int height;

    /// <summary>
    /// The number of levels (floors) in the map.
    /// </summary>
    public int levels;

    /// <summary>
    /// Base path for shader files.
    /// </summary>
    private string shaderPath = "Shaders/";

    // Predefined materials for different world elements
    Material wallMaterial = new Material(new Vector3(0.2f, 0.2f, 0.6f), new Vector3(0.2f), 5f);
    Material doorMaterial = new Material(new Vector3(0.45f, 0.25f, 0.1f), new Vector3(0.1f), 10f);
    Material lampMaterial = new Material(new Vector3(0.8f, 0.8f, 0.7f), new Vector3(0.9f), 64f);
    Material floorMaterial = new Material(new Vector3(0.4f, 0.4f, 0.4f), new Vector3(0.6f), 32f);
    Material ceilMaterial = new Material(new Vector3(0.4f, 0.6f, 0.4f), new Vector3(0.1f), 5f);
    Material teleporterPadMaterial = new Material(new Vector3(0.5f, 0.1f, 0.5f), new Vector3(0.8f), 32f);
    Material invisibleMaterial = new Material(Vector3.Zero, Vector3.Zero, 0f); // Used for invisible collision walls
    Material collectibleMaterial = new Material(new Vector3(1.0f, 0.84f, 0.0f), new Vector3(1.0f), 64f); // Gold-like
    Material objectMaterial = new Material(new Vector3(0.6f, 1.0f, 0.9f), new Vector3(1.0f), 128f); // Generic object material

    // World generation dimensions, likely loaded from a central configuration like Program class
    float cellBlockWidth = Program.WorldCellWidth;
    float cellBlockDepth = Program.WorldCellDepth;
    float currentStoryHeight = Program.WorldStoryHeight;
    float slabHeight = Program.WorldSlabHeight; // Height of floor/ceiling slabs

    /// <summary>
    /// Initializes a new instance of the <see cref="World"/> class by loading map data from the specified file path.
    /// Parses the map file and generates the world structure, models, and lights.
    /// </summary>
    /// <param name="filePath">The path to the map file.</param>
    /// <exception cref="Exception">Throws various exceptions if map loading or parsing fails (e.g., FileNotFoundException, FormatException).</exception>
    public World(string filePath)
    {
        worldModels = new List<Model>();
        worldLights = new List<Light>();
        Teleporters = new List<Teleporter>();
        try
        {
            LoadMap(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL: World constructor failed: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            throw; // Re-throw to indicate critical failure
        }
    }

    /// <summary>
    /// Loads and parses the map data from a text file.
    /// Initializes <see cref="worldData"/>, dimensions, and then calls generation methods.
    /// </summary>
    /// <param name="filePath">The path to the map file.</param>
    /// <exception cref="FileNotFoundException">If the map file does not exist.</exception>
    /// <exception cref="ArgumentException">If the map file is empty, has insufficient lines, or lines are too short.</exception>
    /// <exception cref="FormatException">If map dimensions or other parsable parts are malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If map dimensions are not positive.</exception>
    private void LoadMap(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"ERROR: Map file not found: {filePath}");
                throw new FileNotFoundException("Map file not found.", filePath);
            }
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
            {
                Console.WriteLine($"ERROR: Map file is empty: {filePath}");
                throw new ArgumentException("Map file is empty.");
            }

            // Parse dimensions (Width x Height x Levels) from the first line
            string[] dimensions = lines[0].Split('x');
            if (dimensions.Length < 3)
            {
                Console.WriteLine($"ERROR: Invalid dimensions format in map file header: \"{lines[0]}\". Expected WxHxL (e.g., 5x4x2).");
                throw new FormatException("Invalid dimensions format in map file header.");
            }
            try
            {
                width = int.Parse(dimensions[0]);
                height = int.Parse(dimensions[1]); // This is depth in the map file (rows)
                levels = int.Parse(dimensions[2]);
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"ERROR: Could not parse map dimensions from \"{lines[0]}\": {ex.Message}");
                throw;
            }
            catch (OverflowException ex)
            {
                Console.WriteLine($"ERROR: Map dimension value is too large or too small in \"{lines[0]}\": {ex.Message}");
                throw;
            }

            if (width <= 0 || height <= 0 || levels <= 0)
            {
                Console.WriteLine($"ERROR: Map dimensions must be positive. Got Width:{width}, Height:{height}, Levels:{levels}");
                throw new ArgumentOutOfRangeException("Map dimensions", "Dimensions must be positive integers.");
            }

            worldData = new WorldElement[levels, height, width];
            int expectedMapDataLines = levels * height;
            if (lines.Length < 1 + expectedMapDataLines)
            {
                Console.WriteLine($"ERROR: Not enough lines in map file for defined dimensions. Header + {expectedMapDataLines} data lines expected, found {lines.Length} total lines.");
                throw new ArgumentException("Not enough lines in map file for defined dimensions.");
            }

            // Parse map grid data
            for (int level = 0; level < levels; level++)
            {
                for (int row = 0; row < height; row++)
                {
                    int lineIndex = 1 + row + (level * height); // +1 to skip header, then row, then offset by full level blocks
                    if (lines[lineIndex].Length < width)
                    {
                        Console.WriteLine($"ERROR: Line {lineIndex + 1} in map data is too short for defined width {width}. Found length {lines[lineIndex].Length}, expected {width}.");
                        throw new ArgumentException($"Map data line {lineIndex + 1} is too short.");
                    }
                    for (int col = 0; col < width; col++)
                    {
                        worldData[level, row, col] = GetWorldElementFromChar(lines[lineIndex][col]);
                    }
                }
            }
            Console.WriteLine("Map data parsed successfully.");
            CreateTeleportersFromMapData();
            GenerateWorldModels();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL ERROR during LoadMap execution: {ex.Message}\n{ex.StackTrace}");
            throw; // Re-throw to propagate the error
        }
    }

    /// <summary>
    /// Converts a character from the map file to its corresponding <see cref="WorldElement"/> type.
    /// </summary>
    /// <param name="tile">The character representing a map tile.</param>
    /// <returns>The <see cref="WorldElement"/> corresponding to the input character.</returns>
    private WorldElement GetWorldElementFromChar(char tile)
    {
        if (tile == '@') return WorldElement.StartPosition;
        if (tile == ' ' || (tile >= 'a' && tile <= 'n')) return WorldElement.Empty;
        if (tile >= 'o' && tile <= 'z') return WorldElement.Wall;
        if (tile == '*' || tile == '^' || tile == '!') return WorldElement.Light;
        if (tile >= 'A' && tile <= 'G') return WorldElement.Door;
        if (tile >= 'H' && tile <= 'N') return WorldElement.Object; // Generic interactable/static objects
        if (tile >= 'O' && tile <= 'R') return WorldElement.Enemy;
        if (tile == 'T') return WorldElement.Collectible; // Treasure or key item
        if (tile == '-') return WorldElement.Hole; // A pit or missing floor
        if (tile == '+') return WorldElement.TeleporterPad; // Your character for teleporter pad
        // Add more mappings as needed
        return WorldElement.Custom; // Fallback for unrecognized characters
    }

    /// <summary>
    /// Creates <see cref="Teleporter"/> instances based on <see cref="WorldElement.TeleporterPad"/> elements in the map data.
    /// It defines their links by pairing them up sequentially for two-way teleportation.
    /// Assumes Teleporter's MapCoordinates are (Column, Row, Level) for <see cref="Vector3i"/>.
    /// </summary>
    private void CreateTeleportersFromMapData()
    {
        Teleporters.Clear();
        Console.WriteLine("Creating Teleporter instances from map data...");

        List<Vector3i> padMapLocations = new List<Vector3i>();
        // Find all teleporter pad locations
        for (int level = 0; level < this.levels; level++)
        {
            for (int row = 0; row < this.height; row++)
            {
                for (int col = 0; col < this.width; col++)
                {
                    if (worldData[level, row, col] == WorldElement.TeleporterPad)
                    {
                        padMapLocations.Add(new Vector3i(col, row, level)); // Store as (Column, Row, Level)
                    }
                }
            }
        }
        Console.WriteLine($"Found {padMapLocations.Count} teleporter pad locations in map data.");

        // Pair up teleporter pads
        for (int i = 0; i < padMapLocations.Count - 1; i += 2) // Process in pairs
        {
            Vector3i sourceMapCoords = padMapLocations[i];
            Vector3i targetPartnerMapCoords = padMapLocations[i + 1];

            // Calculate world positions for source pad (visual center) and target player landing spot (feet position)
            Vector3 sourceWorldPadVisualCenter = new Vector3(
                sourceMapCoords.X * cellBlockWidth,                              // X
                -sourceMapCoords.Z * currentStoryHeight + slabHeight / 2f,       // Y (level to Y, visual center of pad)
                sourceMapCoords.Y * cellBlockDepth                               // Z (map row to Z)
            );
            Vector3 targetWorldFeetPosForTp1 = new Vector3(
                targetPartnerMapCoords.X * cellBlockWidth,                      // X
                -targetPartnerMapCoords.Z * currentStoryHeight,                 // Y (player feet level)
                targetPartnerMapCoords.Y * cellBlockDepth                       // Z
            );

            // Calculate world positions for the partner pad
            Vector3 targetPartnerWorldPadVisualCenter = new Vector3(
                targetPartnerMapCoords.X * cellBlockWidth,
                -targetPartnerMapCoords.Z * currentStoryHeight + slabHeight / 2f,
                targetPartnerMapCoords.Y * cellBlockDepth
            );
            Vector3 sourceWorldFeetPosForTp2 = new Vector3( // Landing spot when returning to the first pad
               sourceMapCoords.X * cellBlockWidth,
               -sourceMapCoords.Z * currentStoryHeight,
               sourceMapCoords.Y * cellBlockDepth
           );

            // Create two linked teleporters
            Teleporter tp1 = new Teleporter(sourceMapCoords, sourceWorldPadVisualCenter, targetWorldFeetPosForTp1, 1.0f); // Radius 1.0f for trigger
            Teleporter tp2 = new Teleporter(targetPartnerMapCoords, targetPartnerWorldPadVisualCenter, sourceWorldFeetPosForTp2, 1.0f);

            tp1.Partner = tp2; // Link them
            tp2.Partner = tp1;

            Teleporters.Add(tp1);
            Teleporters.Add(tp2);
            Console.WriteLine($"Linked teleporter from Map {sourceMapCoords} (WorldCenter {sourceWorldPadVisualCenter}) to WorldFeetPos {targetWorldFeetPosForTp1}");
            Console.WriteLine($"Linked teleporter from Map {targetPartnerMapCoords} (WorldCenter {targetPartnerWorldPadVisualCenter}) to WorldFeetPos {sourceWorldFeetPosForTp2}");
        }

        // Handle an odd number of teleporter pads (last one won't be paired)
        if (padMapLocations.Count > 0 && padMapLocations.Count % 2 != 0)
        {
            Vector3i lonePadMapCoords = padMapLocations.Last();
            Vector3 lonePadWorldCenter = new Vector3(
                lonePadMapCoords.X * cellBlockWidth,
                -lonePadMapCoords.Z * currentStoryHeight + slabHeight / 2f,
                lonePadMapCoords.Y * cellBlockDepth);
            // Create a teleporter that, by default, points to itself or has no partner
            Teleporters.Add(new Teleporter(lonePadMapCoords, lonePadWorldCenter, lonePadWorldCenter, 1.0f)); // Points to self
            Console.WriteLine($"Warning: Odd number of teleporter pads ({padMapLocations.Count}). The last one at map {lonePadMapCoords} (WorldCenter {lonePadWorldCenter}) is not paired and currently points to itself.");
        }
        Console.WriteLine($"{Teleporters.Count} Teleporter objects created and potentially linked.");
    }

    /// <summary>
    /// Generates the visual <see cref="Model"/> instances for the world based on the parsed <see cref="worldData"/>.
    /// This includes floors, ceilings, walls, doors, lights, and other objects.
    /// </summary>
    public void GenerateWorldModels()
    {
        Console.WriteLine("Generating world models...");
        worldModels.Clear(); // Clear any existing models if regenerating
        worldLights.Clear();

        for (int level = 0; level < levels; level++)
        {
            float baseCellY = -level * currentStoryHeight; // Y coordinate for the floor of the current level

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    WorldElement element = worldData[level, row, col];
                    Vector3 cellCenterXZ = new Vector3(col * cellBlockWidth, 0, row * cellBlockDepth);
                    Vector3 currentCellBasePosition = cellCenterXZ + new Vector3(0, baseCellY, 0);

                    if (level == 0)
                    {
                        Vector3 ceilCenterPos = cellCenterXZ + new Vector3(0, baseCellY + currentStoryHeight + slabHeight / 2f, 0);
                        worldModels.Add(CreateModel(ceilCenterPos, cellBlockWidth, slabHeight, cellBlockDepth, ceilMaterial, true, 0, 0));
                    }

                    // Add floor or teleporter pad
                    if (element == WorldElement.TeleporterPad)
                    {
                        Vector3 floorCenterPos = cellCenterXZ + new Vector3(0, baseCellY - slabHeight / 2f, 0); // Floor slab center
                        worldModels.Add(CreateModel(floorCenterPos, cellBlockWidth, slabHeight, cellBlockDepth, teleporterPadMaterial, true, 0, 0));
                        Console.WriteLine($"Teleporter Pad model created at L{level},R{row},C{col}");
                    }
                    else if (element != WorldElement.Hole && element != WorldElement.Wall && element != WorldElement.Door) // Don't draw floor under walls/doors if they fill the cell
                    {
                        Vector3 floorCenterPos = cellCenterXZ + new Vector3(0, baseCellY - slabHeight / 2f, 0);
                        worldModels.Add(CreateModel(floorCenterPos, cellBlockWidth, slabHeight, cellBlockDepth, floorMaterial, true, 0, 0));
                    }

                    bool isEdge = (row == 0 || row == height - 1 || col == 0 || col == width - 1);

                    // Process specific elements
                    if (element == WorldElement.StartPosition)
                    {
                        this.StartPos = currentCellBasePosition; // Player starts at floor level
                        this.StartPositionLevel = level;
                        Console.WriteLine($"Start position recorded at L{level},R{row},C{col}. WorldCoords for base: X={this.StartPos.X:F2}, Y={this.StartPos.Y:F2}, Z={this.StartPos.Z:F2}");
                    }
                    else // Handle other elements that are not just floor/ceiling/start
                    {
                        switch (element)
                        {
                            case WorldElement.Wall:
                            case WorldElement.Door:
                                Material currentMaterial = (element == WorldElement.Wall) ? wallMaterial : doorMaterial;
                                bool hasCollision = (element == WorldElement.Wall); // Doors might not collide initially or have different collision
                                Vector3 wallCenterPos = cellCenterXZ + new Vector3(0, baseCellY + currentStoryHeight / 2f, 0); // Center of the wall block
                                worldModels.Add(CreateModel(wallCenterPos, cellBlockWidth, currentStoryHeight, cellBlockDepth, currentMaterial, hasCollision, 0, 0));
                                break;
                            case WorldElement.Light:
                                (Model lightModel, Light light) = CreateLightModel(currentCellBasePosition);
                                worldModels.Add(lightModel);
                                worldLights.Add(light);
                                break;
                            case WorldElement.Collectible:
                                Vector3 collectibleCenterPos = cellCenterXZ + new Vector3(0, baseCellY + 1f, 0); // Position above floor
                                worldModels.Add(CreateModel(collectibleCenterPos, 0.5f, 0.5f, 0.5f, collectibleMaterial, false, 0, 2f)); // Small, rotating
                                break;
                            case WorldElement.Object: // Generic static object
                                Vector3 objectCenterPos = cellCenterXZ + new Vector3(0, baseCellY + 0.5f, 0); // Example: half unit height, on floor
                                worldModels.Add(CreateModel(objectCenterPos, 1f, 1f, 1f, objectMaterial, true, 0, 0)); // Example size
                                break;
                            case WorldElement.Hole: // No model, already handled by not drawing a floor
                            case WorldElement.TeleporterPad: // Model already created
                                break;
                                // Add cases for Enemy, etc. if they have static models generated here
                        }
                    }

                    // Add invisible bounding walls at the edges of the map if the cell is empty
                    if (isEdge && element == WorldElement.Empty)
                    {
                        Vector3 edgeWallCenter = cellCenterXZ + new Vector3(0, baseCellY + currentStoryHeight / 2f, 0);
                        worldModels.Add(CreateModel(edgeWallCenter, cellBlockWidth, currentStoryHeight, cellBlockDepth, invisibleMaterial, true, 0, 0));
                    }
                }
            }
        }
        Console.WriteLine("World models generation complete.");
    }

    /// <summary>
    /// Creates a generic block <see cref="Model"/>.
    /// </summary>
    /// <param name="modelCenterPos">The center position of the model in world coordinates.</param>
    /// <param name="cubeWidth">The width of the model (X-axis).</param>
    /// <param name="cubeHeight">The height of the model (Y-axis).</param>
    /// <param name="cubeDepth">The depth of the model (Z-axis).</param>
    /// <param name="material">The <see cref="Material"/> to apply to the model.</param>
    /// <param name="createCollisionBox">Whether to create an AABB collision box for this model.</param>
    /// <param name="yRotation">Initial Y-axis rotation in radians.</param>
    /// <param name="rotationSpeed">Speed of rotation around the Y-axis (radians per second).</param>
    /// <returns>The created <see cref="Model"/> instance.</returns>
    private Model CreateModel(Vector3 modelCenterPos, float cubeWidth, float cubeHeight, float cubeDepth, Material material, bool createCollisionBox, float yRotation, float rotationSpeed)

    {

        (Vertex[] vertices, int[] indices) = Model.CreateBlock(cubeWidth, cubeHeight, cubeDepth, 0f);

        Model model = new Model(vertices, indices, material);

        model.Shader = new Shader(Path.Combine(shaderPath, "basic.vert"), Path.Combine(shaderPath, "basic.frag"));

        model.position = modelCenterPos;

        model.rotation = new Vector3(0, yRotation, 0);

        model.rotationSpeed = rotationSpeed;

        model.IsCollidable = createCollisionBox;

        if (createCollisionBox)

        {

            Vector3 halfExtents = new Vector3(cubeWidth / 2f, cubeHeight / 2f, cubeDepth / 2f);

            model.CollisionBox = new AABB(modelCenterPos - halfExtents, modelCenterPos + halfExtents);

        }

        return model;

    }

    /// <summary>
    /// Creates a light source model (e.g., a lamp fixture) and its associated <see cref="Light"/> object.
    /// </summary>
    /// <param name="lightCellBasePos">The base position (floor level) of the cell where the light is located.</param>
    /// <returns>A tuple containing the <see cref="Model"/> for the light fixture and the <see cref="Light"/> source.</returns>
    private (Model, Light) CreateLightModel(Vector3 lightCellBasePos)
    {
        float lampSize = 0.3f; // Visual size of the lamp fixture
        // Position the lamp near the ceiling of the current cell
        Vector3 lampCenterPos = lightCellBasePos + new Vector3(0f, currentStoryHeight - lampSize / 2f, 0f);
        Model lightModel = CreateModel(lampCenterPos, lampSize, lampSize, lampSize, lampMaterial, false, 0, 0);

        // Light source slightly below the fixture
        Light light = new Light(lampCenterPos - new Vector3(0f, 0.1f, 0f), false); 
        light.intensity = 10f; // Example intensity
        // light.color can also be set here if needed
        return (lightModel, light);
    }

    /// <summary>
    /// Renders all models in the world using the provided camera and a primary light source.
    /// Note: This method uses a single light for all models. For multiple lights, model drawing logic would need adjustment.
    /// </summary>
    /// <param name="camera">The <see cref="Camera"/> used for viewing the world.</param>
    /// <param name="light">The primary <see cref="Light"/> source for shading. (Consider passing List<Light> if needed).</param>
    public void RenderAllModels(Camera camera, Light light) // Or pass List<Light> worldLights
    {
        if (worldModels == null)
        {
            Console.WriteLine("Warning: worldModels list is null in RenderAllModels.");
            return;
        }
        foreach (var model in worldModels)
        {
            if (model == null)
            {
                Console.WriteLine("Warning: Found a null model in worldModels list.");
                continue;
            }
            // Only draw models with a valid, non-zero diffuse material (e.g., not invisible collision boxes if they have zero diffuse)
            if (model.Material != null && (model.Material.diffuse != Vector3.Zero || model.Material.specular != Vector3.Zero)) // Check if material is not effectively invisible
            {
                // If using multiple lights, the Draw method or shader setup would need to handle them.
                // For now, assuming model.Draw can take the primary light or has access to worldLights.
                model.Draw(camera, light); // This might need to be model.Draw(camera, worldLights);
            }
            else if (model.Material == null)
            {
                Console.WriteLine($"Warning: Model at {model.position} has a null Material and was not drawn.");
            }
        }
    }

    /// <summary>
    /// Checks if the given map grid coordinates (row, col) are within the defined map bounds for a single level.
    /// </summary>
    /// <param name="row">The row index (typically Z-axis in map data).</param>
    /// <param name="col">The column index (typically X-axis in map data).</param>
    /// <returns>True if the coordinates are within bounds, false otherwise.</returns>
    public bool InBounds(int row, int col)
    {
        return row >= 0 && col >= 0 && row < height && col < width;
    }

    /// <summary>
    /// Gets the <see cref="WorldElement"/> type at the specified map coordinates (level, row, col).
    /// </summary>
    /// <param name="level">The level index.</param>
    /// <param name="row">The row index.</param>
    /// <param name="col">The column index.</param>
    /// <returns>The <see cref="WorldElement"/> at the location, or <see cref="WorldElement.Empty"/> if out of bounds.</returns>
    public WorldElement GetTile(int level, int row, int col)
    {
        if (level >= 0 && level < levels && InBounds(row, col))
        {
            return worldData[level, row, col];
        }
        Console.WriteLine($"Warning: GetTile called for out-of-bounds or invalid level. L{level}, R{row}, C{col}");
        return WorldElement.Empty; // Default for out-of-bounds
    }

    /// <summary>
    /// Gets the Y coordinate of the highest solid ground surface (non-hole) at the given world XZ coordinates,
    /// searching downwards from the specified starting level index.
    /// </summary>
    /// <param name="posX">The world X coordinate.</param>
    /// <param name="posZ">The world Z coordinate.</param>
    /// <param name="currentFloor">The level index of current floor.</param>
    /// <returns>The Y coordinate of the ground, or <see cref="0f"/> if no ground is found or out of bounds.</returns>
    public float GetGroundHeight(float posX, float posZ, int currentFloor)
    {
        int col = (int)Math.Floor(posX / cellBlockWidth + 0.5f);
        int row = (int)Math.Floor(posZ / cellBlockDepth + 0.5f);
        if (!InBounds(row, col))
            return 0f;

        int currentIndex = -currentFloor;

        for (int idx = currentIndex; idx < levels; idx++)
        {
            Console.WriteLine($"Floor {currentFloor}: {worldData[idx, row, col]}");
            if (worldData[idx, row, col] != WorldElement.Hole)
            {
                int foundFloor = -idx;
                return foundFloor * currentStoryHeight;
            }
        }

        return 0f;
    }


    /// <summary>
    /// Updates all models in the world that have dynamic behavior, such as rotation.
    /// Called once per frame.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last frame, in seconds.</param>
    public void UpdateAllModels(float deltaTime)
    {
        if (worldModels == null) return;
        foreach (var model in worldModels)
        {
            if (model != null && model.rotationSpeed != 0f)
            {
                Vector3 currentRotation = model.rotation;
                currentRotation.Y += model.rotationSpeed * deltaTime;
                // Normalize angle if necessary, e.g., currentRotation.Y %= MathHelper.TwoPi;
                model.rotation = currentRotation;
            }
            // Add other dynamic updates here if needed (e.g., oscillation, bobbing)
        }
    }

    /// <summary>
    /// Disposes all <see cref="Model"/> instances and clears the list of <see cref="Light"/> sources.
    /// This should be called when the world is being unloaded to free up OpenGL resources.
    /// </summary>
    public void DisposeModelsAndLights()
    {
        Console.WriteLine("Disposing world models and lights...");
        if (worldModels != null)
        {
            foreach (var model in worldModels)
            {
                model?.Dispose(); // Shader inside model should be disposed
            }
            worldModels.Clear();
        }
        if (worldLights != null)
        {
            worldLights.Clear();
        }
        Console.WriteLine("World models and lights disposed/cleared.");
    }
}