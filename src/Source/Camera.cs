using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace semestral
{
    public class Camera
    {
        private float zoom = 1;
        public Vector3 position = new Vector3(0, 0, 5);
        public float rx = 0;
        public float ry = 0;
        public float speed = 1.4f;
        public float sensitivity = 1;
        public float radius = 0.3f; // <-- nová vlastnost hráčova "hitboxu"

        public Viewport viewport;
        public Camera(Viewport viewport)
        {
            this.viewport = viewport;
        }

        public virtual Matrix4 Projection
        {
            get
            {
                float ratio = (float)((viewport.Width * viewport.window.ClientSize.X) / (viewport.Height * viewport.window.ClientSize.Y));
                return Matrix4.CreatePerspectiveFieldOfView(zoom, ratio, 0.1f, 50);
            }
        }

        public void Zoom(float coef)
        {
            zoom += coef / 10.0f;
            zoom = Math.Max(0.2f, Math.Min(2, zoom));
            Console.WriteLine(zoom);
        }

        public void Move(float xdt, float ydt)
        {
            position.X += speed * (float)(xdt * Math.Cos(ry) + ydt * Math.Sin(ry));
            position.Z += speed * (float)(xdt * Math.Sin(ry) - ydt * Math.Cos(ry));
        }

        public void TryMove(Vector3 direction, float deltaTime, List<Model> obstacles)
        {
            Vector3 nextPos = position + direction * speed * deltaTime;

            if (!IsCollision(nextPos, radius, obstacles))
            {
                position = nextPos;
                // Console.WriteLine($"Hráč se posunul na: X={position.X}, Y={position.Y}, Z={position.Z}");
                return;
            }

            Vector3 slideX = new Vector3(direction.X, 0, 0).Normalized() * speed * deltaTime;
            Vector3 slideZ = new Vector3(0, 0, direction.Z).Normalized() * speed * deltaTime;

            if (!IsCollision(position + slideX, radius, obstacles))
            {
                position += slideX;
                //Console.WriteLine($"Hráč se posunul na: X={position.X}, Y={position.Y}, Z={position.Z} (slideX)"); // Debug výpis
            }
            else if (!IsCollision(position + slideZ, radius, obstacles))
            {
                position += slideZ;
                //Console.WriteLine($"Hráč se posunul na: X={position.X}, Y={position.Y}, Z={position.Z} (slideZ)"); // Debug výpis
            }
        }

        private bool IsCollision(Vector3 pos, float radius, List<Model> obstacles)
        {
            foreach (var model in obstacles)
            {
                if (model.CollisionBox.playerIntersect(pos, radius)) return true;
            }
            return false;
        }

        public void RotateX(float a)
        {
            rx += a * sensitivity;

            float epsilon = 0.001f; 

            float pitchLimitRadians = (float)(Math.PI / 2.0 - epsilon);

            rx = MathHelper.Clamp(rx, -pitchLimitRadians, pitchLimitRadians);
        }

        public void RotateY(float a)
        {
            ry += a * sensitivity;
        }

        public virtual Matrix4 View
        {
            get
            {
                Matrix4 view;
                view = Matrix4.Identity;
                view *= Matrix4.CreateTranslation(-position);
                view *= Matrix4.CreateRotationY(ry);
                view *= Matrix4.CreateRotationX(rx);
                return view;
            }
        }

        public Vector3 GetDirection()
        {
            return new Vector3(
                (float)Math.Sin(ry) * (float)Math.Cos(rx),
                -(float)Math.Sin(rx),
                -(float)Math.Cos(ry) * (float)Math.Cos(rx)
            );
        }
    }
}
