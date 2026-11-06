using System;
using System.Runtime.InteropServices;

using UVec3f = UnityEngine.Vector3;

namespace PipeWorld.Piper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vec3f : IEquatable<Vec3f>
    {
        public float x, y, z;

        public Vec3f(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vec3f(double x, double y, double z) { this.x = (float)x; this.y = (float)y; this.z = (float)z; }

    #if UNITY_5_3_OR_NEWER
        public static implicit operator UVec3f(Vec3f v) { return new UVec3f(v.x, v.y, v.z); }
        public static implicit operator Vec3f(UVec3f v) { return new Vec3f(v.x, v.y, v.z); }

        public static bool operator ==(UVec3f left, Vec3f right) { return left.x == right.x && left.y == right.y && left.z == right.z; }
        public static bool operator !=(UVec3f left, Vec3f right) { return left.x != right.x || left.y != right.y || left.z != right.z; }
        public static Vec3f operator-(UVec3f a, Vec3f b) { return new Vec3f(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vec3f operator+(UVec3f a, Vec3f b) { return new Vec3f(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static bool operator ==(Vec3f left, UVec3f right) { return left.x == right.x && left.y == right.y && left.z == right.z; }
        public static bool operator !=(Vec3f left, UVec3f right) { return left.x != right.x || left.y != right.y || left.z != right.z; }
        public static Vec3f operator-(Vec3f a, UVec3f b) { return new Vec3f(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vec3f operator+(Vec3f a, UVec3f b) { return new Vec3f(a.x + b.x, a.y + b.y, a.z + b.z); }
    #endif

        public bool Equals(Vec3f other) { return x == other.x && y == other.y && z == other.z; }
        public override int GetHashCode() { return HashCode.Combine(x, y, z); }
        public static bool operator ==(Vec3f left, Vec3f right) { return left.x == right.x && left.y == right.y && left.z == right.z; }
        public static bool operator !=(Vec3f left, Vec3f right) { return left.x != right.x || left.y != right.y || left.z != right.z; }
        public static Vec3f operator-(Vec3f a, Vec3f b) { return new Vec3f(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vec3f operator+(Vec3f a, Vec3f b) { return new Vec3f(a.x + b.x, a.y + b.y, a.z + b.z); }

        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);

        public static readonly Vec3f INVALID = new Vec3f(float.MaxValue, float.MaxValue, float.MaxValue);
    }
}
