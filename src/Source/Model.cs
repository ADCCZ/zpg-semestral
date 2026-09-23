using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace semestral
{
    /// <summary>
    /// Jednoduchý model obsahující seznam vrcholů
    /// </summary>
    public class Model : IDisposable
    {
        public Shader Shader { get; set; }
        public Material Material { get; set; }
        public Vector3 position { get; set; }
        public AABB CollisionBox { get; set; }
        public bool IsCollidable { get; set; }

        int vbo;
        int ibo;
        int vao;

        int triangles;

        public Vector3 rotation = Vector3.Zero;
        public float rotationSpeed = 0f;

        public Model(Vertex[] vertices, int[] indices, Material material)
        {
            triangles = indices.Length / 3;
            Material = material;
            IsCollidable = false;

            // vytvoření a připojení VAO
            GL.GenVertexArrays(1, out vao);
            GL.BindVertexArray(vao);

            // Vytvoření a připojení VBO
            vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * Vertex.SizeOf(), vertices, BufferUsageHint.StaticDraw);

            // vytvoření a připojení IBO
            ibo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

            // namapování pointerů na lokace v shaderu
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Vertex.SizeOf(), IntPtr.Zero);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Vertex.SizeOf(), (IntPtr)3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Vykreslení modelu
        /// </summary>
        public void Draw(Camera camera, Light light)
        {
            Matrix4 rotationMatrix = Matrix4.CreateRotationX(rotation.X) *
                                     Matrix4.CreateRotationY(rotation.Y) *
                                     Matrix4.CreateRotationZ(rotation.Z);
            Matrix4 translationMatrix = Matrix4.CreateTranslation(position);
            Matrix4 modelMatrix = rotationMatrix * translationMatrix;

            Shader.Use();
            Shader.SetUniform("projection", camera.Projection);
            Shader.SetUniform("view", camera.View);
            Shader.SetUniform("model", modelMatrix);
            Shader.SetUniform("cameraPosWorld", camera.position);

            Shader.SetUniform("lightPosWorld", light.position.Xyz);
            Shader.SetUniform("lightDirWorld", -light.direction);

            Shader.SetUniform("lightColor", light.color);
            Shader.SetUniform("lightIntensity", light.intensity);
            Shader.SetUniform("lightOn", 1);
            Shader.SetUniform("cutOff", (float)Math.Cos(2 * Math.PI / 180));

            Material.SetUniforms(Shader);

            GL.BindVertexArray(vao);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 3 * triangles, DrawElementsType.UnsignedInt, IntPtr.Zero);
            GL.BindVertexArray(0);
        }

        public static (Vertex[] vertices, int[] indices) CreateBlock(float width, float height, float depth, float verticalOffset)
        {
            float halfWidth = width / 2f;
            float halfDepth = depth / 2f;
            float halfHeight = height / 2f;

            Vertex[] vertices = new Vertex[24];

            // Definice normál pro každou stěnu
            Vector3 normFront = new Vector3(0, 0, 1);
            Vector3 normRight = new Vector3(1, 0, 0);
            Vector3 normBack = new Vector3(0, 0, -1);
            Vector3 normLeft = new Vector3(-1, 0, 0);
            Vector3 normTop = new Vector3(0, 1, 0);
            Vector3 normBottom = new Vector3(0, -1, 0);

            // Front (+Z)
            vertices[0] = new Vertex(new Vector3(-halfWidth, -halfHeight + verticalOffset, halfDepth), normFront);
            vertices[1] = new Vertex(new Vector3(halfWidth, -halfHeight + verticalOffset, halfDepth), normFront);
            vertices[2] = new Vertex(new Vector3(halfWidth, halfHeight + verticalOffset, halfDepth), normFront);
            vertices[3] = new Vertex(new Vector3(-halfWidth, halfHeight + verticalOffset, halfDepth), normFront);

            // Right (+X)
            vertices[4] = new Vertex(new Vector3(halfWidth, -halfHeight + verticalOffset, halfDepth), normRight);
            vertices[5] = new Vertex(new Vector3(halfWidth, -halfHeight + verticalOffset, -halfDepth), normRight);
            vertices[6] = new Vertex(new Vector3(halfWidth, halfHeight + verticalOffset, -halfDepth), normRight);
            vertices[7] = new Vertex(new Vector3(halfWidth, halfHeight + verticalOffset, halfDepth), normRight);

            // Back (-Z)
            vertices[8] = new Vertex(new Vector3(halfWidth, -halfHeight + verticalOffset, -halfDepth), normBack);
            vertices[9] = new Vertex(new Vector3(-halfWidth, -halfHeight + verticalOffset, -halfDepth), normBack);
            vertices[10] = new Vertex(new Vector3(-halfWidth, halfHeight + verticalOffset, -halfDepth), normBack);
            vertices[11] = new Vertex(new Vector3(halfWidth, halfHeight + verticalOffset, -halfDepth), normBack);

            // Left (-X)
            vertices[12] = new Vertex(new Vector3(-halfWidth, -halfHeight + verticalOffset, -halfDepth), normLeft);
            vertices[13] = new Vertex(new Vector3(-halfWidth, -halfHeight + verticalOffset, halfDepth), normLeft);
            vertices[14] = new Vertex(new Vector3(-halfWidth, halfHeight + verticalOffset, halfDepth), normLeft);
            vertices[15] = new Vertex(new Vector3(-halfWidth, halfHeight + verticalOffset, -halfDepth), normLeft);

            // Top (+Y)
            vertices[16] = new Vertex(new Vector3(-halfWidth, halfHeight + verticalOffset, halfDepth), normTop);
            vertices[17] = new Vertex(new Vector3(halfWidth, halfHeight + verticalOffset, halfDepth), normTop);
            vertices[18] = new Vertex(new Vector3(halfWidth, halfHeight + verticalOffset, -halfDepth), normTop);
            vertices[19] = new Vertex(new Vector3(-halfWidth, halfHeight + verticalOffset, -halfDepth), normTop);

            // Bottom (-Y)
            vertices[20] = new Vertex(new Vector3(-halfWidth, -halfHeight + verticalOffset, -halfDepth), normBottom);
            vertices[21] = new Vertex(new Vector3(halfWidth, -halfHeight + verticalOffset, -halfDepth), normBottom);
            vertices[22] = new Vertex(new Vector3(halfWidth, -halfHeight + verticalOffset, halfDepth), normBottom);
            vertices[23] = new Vertex(new Vector3(-halfWidth, -halfHeight + verticalOffset, halfDepth), normBottom);

            int[] indices = new int[36];

            indices[0] = 0; indices[1] = 1; indices[2] = 2;
            indices[3] = 0; indices[4] = 2; indices[5] = 3;

            indices[6] = 4; indices[7] = 5; indices[8] = 6;
            indices[9] = 4; indices[10] = 6; indices[11] = 7;

            indices[12] = 8; indices[13] = 9; indices[14] = 10;
            indices[15] = 8; indices[16] = 10; indices[17] = 11;

            indices[18] = 12; indices[19] = 13; indices[20] = 14;
            indices[21] = 12; indices[22] = 14; indices[23] = 15;

            indices[24] = 16; indices[25] = 17; indices[26] = 18;
            indices[27] = 16; indices[28] = 18; indices[29] = 19;

            indices[30] = 20; indices[31] = 21; indices[32] = 22;
            indices[33] = 20; indices[34] = 22; indices[35] = 23;

            return (vertices, indices);
        }

        #region Dispose - uvolnění paměti
        bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Shader?.Dispose();
                }

                GL.DeleteBuffer(vbo);
                GL.DeleteBuffer(ibo);
                GL.DeleteVertexArray(vao);
                Console.WriteLine($"Model (VAO: {vao}) OpenGL resources released.");

                disposed = true;
            }
        }

        ~Model() => Dispose(false);
        #endregion
    }
}
