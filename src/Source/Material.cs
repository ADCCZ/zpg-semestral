using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace semestral
{
    public class Material
    {
        public Material(Vector3 diffuse, Vector3 specular, float shininess)
        {
            this.diffuse = diffuse;
            this.specular = specular;
            this.shininess = shininess;
        }

        public Vector3 diffuse = new Vector3(0.5f, 0.01f, 0.01f);
        public Vector3 specular = new Vector3(0.8f);
        public float shininess = 32.0f;

        public void SetUniforms(Shader shader)
        {
            shader.SetUniform("material.diffuse", diffuse);
            shader.SetUniform("material.specular", specular);
            shader.SetUniform("material.shininess", shininess);
        }
    }
}
