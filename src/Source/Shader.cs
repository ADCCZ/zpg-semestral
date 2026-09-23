using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace semestral;
/// <summary>
/// Handles shader loading and working with them
/// </summary>
public class Shader : IDisposable
{
    public int ID { get; private set; }

    private readonly Dictionary<string, int> uniforms = new();

    public Shader(string vertexPath, string fragmentPath)
    {
        int vertexShader = CompileShader(vertexPath, ShaderType.VertexShader);
        int fragmentShader = CompileShader(fragmentPath, ShaderType.FragmentShader);
        LinkShader(vertexShader, fragmentShader);

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        LoadUniforms();
    }

    /// <summary>
    /// Shader Compilation
    /// </summary>
    /// <param name="filePath">Path to the shader file</param>
    /// <param name="type">Type of the shader</param>
    /// <returns>Compiled shader ID</returns>
    /// <exception cref="Exception">Thrown if shader compilation fails</exception>
    private int CompileShader(string filePath, ShaderType type)
    {
        string source = File.ReadAllText(filePath);
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        if (success == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            Console.WriteLine($"Error compiling {type} shader ({filePath}):\n{log}\n");
            throw new Exception($"Shader compilation failed for {filePath}");
        }

        return shader;
    }

    /// <summary>
    /// Links shaders into the resulting program.
    /// </summary>
    /// <param name="shaders">Shader IDs to link</param>
    /// <exception cref="Exception">Thrown if shader program linking fails</exception>
    private void LinkShader(params int[] shaders)
    {
        ID = GL.CreateProgram();
        foreach (int shader in shaders)
        {
            GL.AttachShader(ID, shader);
        }
        GL.LinkProgram(ID);

        GL.GetProgram(ID, GetProgramParameterName.LinkStatus, out int success);
        if (success == 0)
        {
            string log = GL.GetProgramInfoLog(ID);
            throw new Exception($"Error linking shader program:\n{log}");
        }
    }

    /// <summary>
    /// Uses the program. The program must be used first, then uniforms can be set.
    /// </summary>
    public void Use()
    {
        GL.UseProgram(ID);
    }

    /// <summary>
    /// Loads the locations of all uniforms from the shader.
    /// </summary>
    private void LoadUniforms()
    {
        GL.GetProgram(ID, GetProgramParameterName.ActiveUniforms, out int uniformCount);

        for (int i = 0; i < uniformCount; i++)
        {
            GL.GetActiveUniform(ID, i, 256, out _, out _, out _, out string name);

            string uniformName = name.ToString();
            int location = GL.GetUniformLocation(ID, uniformName);

            if (location != -1)
            {
                uniforms[uniformName] = location;
                Console.WriteLine($"Loaded uniform: {uniformName} -> {location}");
            }
        }
    }

    /// <summary>
    /// Gets the location of a uniform variable from the dictionary, if it exists.
    /// </summary>
    /// <param name="name">Name of the uniform</param>
    /// <returns>Location of the uniform or -1 if not found</returns>
    public int GetUniformLocation(string name)
    {
        if (uniforms.TryGetValue(name, out int location))
            return location;

        Console.WriteLine($"Warning: Uniform '{name}' not found.");
        return -1;
    }

    /// <summary>
    /// Intelligent method for setting uniform variables.
    /// </summary>
    public void SetUniform<T>(string name, T value)
    {
        int location = GetUniformLocation(name);
        if (location == -1) return;

        switch (value)
        {
            case int v:
                GL.Uniform1(location, v);
                break;
            case float v:
                GL.Uniform1(location, v);
                break;
            case Vector2 v:
                GL.Uniform2(location, v);
                break;
            case OpenTK.Mathematics.Vector3 v: // Kept original namespace for clarity
                GL.Uniform3(location, v);
                break;
            case Vector4 v:
                GL.Uniform4(location, v);
                break;
            case Matrix4 v:
                GL.UniformMatrix4(location, false, ref v);
                break;
            default:
                throw new NotSupportedException($"Uniform type {typeof(T)} is not supported.");
        }
    }


    #region IDisposable
    private bool disposed = false;

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
                // managed resources cleanup
            }

            if (GL.IsProgram(ID))
                GL.DeleteProgram(ID);

            disposed = true;
            Console.WriteLine("Shader program deleted.");
        }
    }


    ~Shader()
    {
        Dispose(false);
    }
    #endregion
}