using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace semestral
{
    /// <summary>
    /// Represents and renders a 2D minimap of the game world.
    /// It displays the layout of the current level and the player's position.
    /// </summary>
    public class Minimap : IDisposable
    {
        /// <summary>
        /// Shader program used for rendering the minimap elements.
        /// </summary>
        private Shader shader;
        /// <summary>
        /// Vertex Array Object (VAO) for the minimap grid cells.
        /// </summary>
        private int vao;
        /// <summary>
        /// Vertex Buffer Object (VBO) for the minimap grid cells.
        /// </summary>
        private int vbo;

        /// <summary>
        /// Vertex data for a single quad used to represent a cell on the minimap.
        /// Defines two triangles.
        /// </summary>
        private readonly float[] quadVertices =
        {
            // posX, posY
            -0.5f, -0.5f,
             0.5f, -0.5f,
             0.5f,  0.5f,
             0.5f,  0.5f,
            -0.5f,  0.5f,
            -0.5f, -0.5f
        };

        /// <summary>
        /// Vertex Array Object (VAO) for the player indicator arrow.
        /// </summary>
        private int arrowVao;
        /// <summary>
        /// Vertex Buffer Object (VBO) for the player indicator arrow.
        /// </summary>
        private int arrowVbo;
        /// <summary>
        /// Vertex data for the player indicator arrow (a triangle).
        /// </summary>
        private readonly float[] arrowVertices = {
            // x, y
             0.0f,  0.7f,  // Tip (top)
            -0.4f, -0.4f,  // Bottom left corner
             0.4f, -0.4f   // Bottom right corner
        };

        /// <summary>
        /// Flag indicating whether OpenGL resources for the minimap have been successfully initialized.
        /// </summary>
        private bool resourcesInitialized = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Minimap"/> class.
        /// Loads shaders and sets up OpenGL buffers for rendering.
        /// </summary>
        /// <param name="vertexShaderPath">The file path to the vertex shader.</param>
        /// <param name="fragmentShaderPath">The file path to the fragment shader.</param>
        public Minimap(string vertexShaderPath, string fragmentShaderPath)
        {
            try
            {
                shader = new Shader(vertexShaderPath, fragmentShaderPath);

                vao = GL.GenVertexArray();
                vbo = GL.GenBuffer();

                GL.BindVertexArray(vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

                GL.EnableVertexAttribArray(0); // Position attribute
                GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                GL.BindVertexArray(0);

                // Initialize arrow (triangle)
                arrowVao = GL.GenVertexArray();
                arrowVbo = GL.GenBuffer();

                GL.BindVertexArray(arrowVao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, arrowVbo);
                GL.BufferData(BufferTarget.ArrayBuffer, arrowVertices.Length * sizeof(float), arrowVertices, BufferUsageHint.StaticDraw);

                GL.EnableVertexAttribArray(0); // Position attribute
                GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                GL.BindVertexArray(0);
                resourcesInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to initialize Minimap resources: {ex.Message}");
                resourcesInitialized = false;
            }
        }

        /// <summary>
        /// Renders the minimap based on the current world state and camera position.
        /// </summary>
        /// <param name="world">The game world data.</param>
        /// <param name="camera">The player's camera.</param>
        /// <param name="viewport">The viewport settings for rendering the minimap.</param>
        /// <param name="playerHeight">The height of the player character.</param>
        /// <param name="storyHeight">The height of one level/story in the game world.</param>
        /// <param name="cellRenderWidth">The width of a single cell in world units for rendering purposes.</param>
        /// <param name="cellRenderDepth">The depth of a single cell in world units for rendering purposes.</param>
        public void Render(World world, Camera camera, Viewport viewport,
                           float playerHeight, float storyHeight, float cellRenderWidth, float cellRenderDepth)
        {
            if (!resourcesInitialized || shader == null) return;

            viewport.Set();
            GL.Disable(EnableCap.DepthTest); // Minimap is 2D, drawn on top

            float displayRange = 4 * 1.42f; // Defines the visible area of the minimap, ~ sqrt(2) * some_factor

            // Orthographic projection for 2D rendering
            Matrix4 projection = Matrix4.CreateOrthographicOffCenter(
                -displayRange, displayRange,
                -displayRange, displayRange,
                -1f, 1f); // Near and far planes for ortho projection

            // View matrix centers on the player and rotates with the camera's Y rotation
            // Y is flipped because screen coordinates usually have Y down, but our geometry might be Y up.
            Matrix4 view = Matrix4.CreateScale(1f, -1f, 1f) * Matrix4.CreateRotationZ(camera.ry);

            shader.Use();
            shader.SetUniform("projection", projection);
            shader.SetUniform("view", view);

            float playerFeetY = camera.position.Y - playerHeight;

            // Determine the current level of the player
            int playerCurrentLevel = (int)Math.Round(-playerFeetY / storyHeight);
            playerCurrentLevel = Math.Clamp(playerCurrentLevel, 0, world.levels - 1);

            // Determine the player's current grid cell
            Vector2i playerGridCell = new Vector2i(
                (int)Math.Floor(camera.position.X / cellRenderWidth + 0.5f),
                (int)Math.Floor(camera.position.Z / cellRenderDepth + 0.5f)
            );

            GL.BindVertexArray(vao); // Bind VAO for map tiles

            int renderRadius = 3; // How many cells to render around the player
            for (int dz_offset = -renderRadius; dz_offset <= renderRadius; dz_offset++)
            {
                for (int dx_offset = -renderRadius; dx_offset <= renderRadius; dx_offset++)
                {
                    int targetGridCol = playerGridCell.X + dx_offset;
                    int targetGridRow = playerGridCell.Y + dz_offset;

                    if (!world.InBounds(targetGridRow, targetGridCol)) continue;

                    WorldElement tile = world.GetTile(playerCurrentLevel, targetGridRow, targetGridCol);

                    // Determine color based on tile type
                    Vector3 color = tile switch
                    {
                        WorldElement.Wall => new Vector3(0.2f, 0.2f, 0.8f),     // Blue
                        WorldElement.Collectible => new Vector3(1.0f, 0.84f, 0.0f),// Gold/Yellow
                        WorldElement.Object => new Vector3(0.5f, 1.0f, 0.5f),      // Green
                        WorldElement.Light => new Vector3(1.0f, 1.0f, 0.8f),      // Light Yellow
                        WorldElement.Door => new Vector3(0.6f, 0.3f, 0.0f),       // Brown
                        WorldElement.Enemy => new Vector3(1.0f, 0.0f, 0.0f),       // Red (for enemy)
                        WorldElement.StartPosition => new Vector3(0.0f, 1.0f, 1.0f),// Turquoise
                        WorldElement.Hole => new Vector3(0.1f, 0.1f, 0.1f),       // Dark Gray (hole)
                        WorldElement.TeleporterPad => new Vector3(0.8f, 0.0f, 0.8f),// Purple/Magenta
                        _ => new Vector3(0.3f, 0.3f, 0.3f)                         // Default/Empty - darker gray
                    };

                    // Model matrix for each tile: scale and translate relative to player's grid position
                    Matrix4 tileModelMatrix = Matrix4.CreateScale(0.85f) * // Slightly smaller than full cell
                                              Matrix4.CreateTranslation(dx_offset, dz_offset, 0.0f);
                    shader.SetUniform("model", tileModelMatrix);
                    shader.SetUniform("color", color);
                    GL.DrawArrays(PrimitiveType.Triangles, 0, 6); // Draw the quad (2 triangles)
                }
            }

            // Render player arrow
            // Model matrix for the arrow: scale, rotate with camera, and translate to center (player's position)
            // The Z translation (0.1f) ensures it's drawn on top of the tiles if depth testing were enabled.
            Matrix4 arrowModelMatrix = Matrix4.CreateScale(-0.5f, -0.5f, 1.0f) * // Scale and flip arrow
                                       Matrix4.CreateRotationZ(camera.ry) * // Align with camera (world view has Z up for minimap)
                                       Matrix4.CreateTranslation(0.0f, 0.0f, 0.1f); // Center of minimap, slightly above tiles
            shader.SetUniform("model", arrowModelMatrix);
            shader.SetUniform("color", new Vector3(1.0f, 0.0f, 0.0f)); // Red arrow

            GL.BindVertexArray(arrowVao); // Bind VAO for the arrow
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3); // Draw the arrow (1 triangle)

            GL.BindVertexArray(0); // Unbind VAO
            GL.Enable(EnableCap.DepthTest); // Re-enable depth testing for the main scene
        }

        /// <summary>
        /// Flag to detect redundant calls to Dispose.
        /// </summary>
        private bool disposedValue = false;

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources;
        /// false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    shader?.Dispose();
                }

                // Free unmanaged resources (OpenGL objects) if they were initialized
                if (resourcesInitialized)
                {
                    GL.DeleteBuffer(vbo);
                    GL.DeleteVertexArray(vao);
                    GL.DeleteBuffer(arrowVbo);
                    GL.DeleteVertexArray(arrowVao);
                    Console.WriteLine("Minimap OpenGL resources released.");
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// Public method to dispose of the minimap's resources.
        /// Implements the IDisposable interface.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer for the <see cref="Minimap"/> class.
        /// Ensures that unmanaged OpenGL resources are released if Dispose was not called explicitly.
        /// </summary>
        ~Minimap()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(false);
        }
    }
}