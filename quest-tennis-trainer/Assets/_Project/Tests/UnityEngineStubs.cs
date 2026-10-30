#if !UNITY_5_3_OR_NEWER
using System;
using System.Reflection;
using NUnitLite;

namespace QuestTennisTrainer.Tests
{
    /// <summary>
    /// Self-executing NUnitLite entrypoint for the hardened distroless container sandbox
    /// while remaining 100% compatible with standard `dotnet test`.
    /// </summary>
    public static class SandboxTestRunner
    {
        public static int Main(string[] args)
        {
            return new AutoRun(typeof(SandboxTestRunner).GetTypeInfo().Assembly).Execute(args);
        }
    }
}

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class HeaderAttribute : Attribute
    {
        public string header { get; }
        public HeaderAttribute(string header) => this.header = header;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class TooltipAttribute : Attribute
    {
        public string tooltip { get; }
        public TooltipAttribute(string tooltip) => this.tooltip = tooltip;
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;

        public static float Abs(float f) => Math.Abs(f);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Acos(float f) => (float)Math.Acos(Clamp(f, -1f, 1f));
        public static float Floor(float f) => (float)Math.Floor(f);
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);
        public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }

    [Serializable]
    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => new Vector2(0f, 0f);
        public float magnitude => Mathf.Sqrt(x * x + y * y);
        public static float Distance(Vector2 a, Vector2 b) =>
            Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    }

    [Serializable]
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 left => new Vector3(-1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 back => new Vector3(0f, 0f, -1f);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);

        public Vector3 normalized
        {
            get
            {
                float mag = magnitude;
                return mag > 1e-5f ? this / mag : zero;
            }
        }

        public static float Dot(Vector3 lhs, Vector3 rhs) =>
            lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;

        public static Vector3 Cross(Vector3 lhs, Vector3 rhs) =>
            new Vector3(
                lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.x * rhs.y - lhs.y * rhs.x);

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t,
                a.z + (b.z - a.z) * t);
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
    }

    [Serializable]
    public struct Quaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        public static Quaternion AngleAxis(float angleDeg, Vector3 axis)
        {
            Vector3 norm = axis.normalized;
            float halfRad = angleDeg * Mathf.Deg2Rad * 0.5f;
            float s = Mathf.Sin(halfRad);
            return new Quaternion(norm.x * s, norm.y * s, norm.z * s, Mathf.Cos(halfRad));
        }

        public static Quaternion Euler(float xDeg, float yDeg, float zDeg)
        {
            return AngleAxis(yDeg, Vector3.up) * AngleAxis(xDeg, Vector3.right) * AngleAxis(zDeg, Vector3.forward);
        }

        public static Quaternion Inverse(Quaternion q)
        {
            float normSq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (normSq < 1e-6f) return identity;
            return new Quaternion(-q.x / normSq, -q.y / normSq, -q.z / normSq, q.w / normSq);
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            float cosHalfTheta = a.w * b.w + a.x * b.x + a.y * b.y + a.z * b.z;
            if (cosHalfTheta < 0f)
            {
                b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
                cosHalfTheta = -cosHalfTheta;
            }
            if (cosHalfTheta >= 0.9995f)
            {
                return new Quaternion(
                    Mathf.Lerp(a.x, b.x, t),
                    Mathf.Lerp(a.y, b.y, t),
                    Mathf.Lerp(a.z, b.z, t),
                    Mathf.Lerp(a.w, b.w, t));
            }
            float halfTheta = Mathf.Acos(cosHalfTheta);
            float sinHalfTheta = Mathf.Sqrt(1f - cosHalfTheta * cosHalfTheta);
            float ratioA = Mathf.Sin((1f - t) * halfTheta) / sinHalfTheta;
            float ratioB = Mathf.Sin(t * halfTheta) / sinHalfTheta;
            return new Quaternion(
                a.x * ratioA + b.x * ratioB,
                a.y * ratioA + b.y * ratioB,
                a.z * ratioA + b.z * ratioB,
                a.w * ratioA + b.w * ratioB);
        }

        public void ToAngleAxis(out float angleDeg, out Vector3 axis)
        {
            float clampedW = Mathf.Clamp(w, -1f, 1f);
            angleDeg = 2f * Mathf.Acos(clampedW) * Mathf.Rad2Deg;
            float s = Mathf.Sqrt(1f - clampedW * clampedW);
            axis = s < 0.001f ? Vector3.right : new Vector3(x / s, y / s, z / s);
        }

        public static Quaternion operator *(Quaternion lhs, Quaternion rhs) =>
            new Quaternion(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);

        public static Vector3 operator *(Quaternion rotation, Vector3 point)
        {
            Vector3 u = new Vector3(rotation.x, rotation.y, rotation.z);
            float s = rotation.w;
            return 2f * Vector3.Dot(u, point) * u
                 + (s * s - Vector3.Dot(u, u)) * point
                 + 2f * s * Vector3.Cross(u, point);
        }
    }

    [Serializable]
    public struct Rect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public bool Contains(Vector2 point) =>
            point.x >= x && point.x <= (x + width) &&
            point.y >= y && point.y <= (y + height);
    }

    public static class Time
    {
        public static float fixedDeltaTime { get; set; } = 1f / 120f;
    }

    public static class Random
    {
        private static readonly System.Random Rng = new System.Random(42);
        public static float Range(float minInclusive, float maxInclusive) =>
            minInclusive + (float)Rng.NextDouble() * (maxInclusive - minInclusive);
    }

    public class Transform
    {
        public Vector3 position { get; set; } = Vector3.zero;
        public Quaternion rotation { get; set; } = Quaternion.identity;
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 right => rotation * Vector3.right;

        public Vector3 TransformPoint(Vector3 localPoint) => position + (rotation * localPoint);
        public void SetPositionAndRotation(Vector3 pos, Quaternion rot)
        {
            position = pos;
            rotation = rot;
        }
    }

    public class MonoBehaviour
    {
        public Transform transform { get; } = new Transform();
    }

    public class AudioClip { }

    public class AudioSource
    {
        public float pitch { get; set; } = 1f;
        public void PlayOneShot(AudioClip clip, float volumeScale = 1f) { }
    }
}

namespace UnityEngine.XR
{
    public enum XRNode
    {
        LeftEye,
        RightEye,
        CenterEye,
        Head,
        LeftHand,
        RightHand
    }

    public struct HapticCapabilities
    {
        public bool supportsImpulse;
        public bool supportsBuffer;
        public uint bufferFrequencyHz;
    }

    public struct InputDevice
    {
        public bool isValid => false;
        public bool TryGetHapticCapabilities(out HapticCapabilities capabilities)
        {
            capabilities = default;
            return false;
        }
        public bool SendHapticBuffer(uint channel, byte[] buffer) => false;
        public bool SendHapticImpulse(uint channel, float amplitude, float duration) => false;
    }

    public static class InputDevices
    {
        public static InputDevice GetDeviceAtXRNode(XRNode node) => default;
    }
}
#endif

