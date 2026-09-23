using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

// AABB (Axis-Aligned Bounding Box) je jednoduchý typ pro reprezentaci ohraničujícího boxu v 3D prostoru.

namespace semestral
{
    public struct AABB
    {
        public Vector3 Min;
        public Vector3 Max;

        public AABB(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public bool Intersects(AABB other)
        {
            return (Min.X <= other.Max.X && Max.X >= other.Min.X) &&
                   (Min.Y <= other.Max.Y && Max.Y >= other.Min.Y) &&
                   (Min.Z <= other.Max.Z && Max.Z >= other.Min.Z);
        }

        public bool playerIntersect(Vector3 sphereCenter, float radius)
        {
            Vector3 closestPoint = new Vector3(
                Math.Max(this.Min.X, Math.Min(sphereCenter.X, this.Max.X)),
                Math.Max(this.Min.Y, Math.Min(sphereCenter.Y, this.Max.Y)),
                Math.Max(this.Min.Z, Math.Min(sphereCenter.Z, this.Max.Z))
            );

            float distance = (sphereCenter - closestPoint).Length;
            return distance < radius;
        }
    }
}
