using System.Text;
using UnityEngine;
using UnityEngine.Pool;

namespace NetworkingPrototype
{
    public static class StringBuilderPool
    {
        private static ObjectPool<StringBuilder> s_Pool;

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            Application.quitting += OnQuit;
            s_Pool = new ObjectPool<StringBuilder>(() => new StringBuilder(), null, sb => sb.Clear());
        }

        private static void OnQuit()
        {
            s_Pool = null;
        }

        public static StringBuilder Get() => s_Pool.Get();
        
        public static void Release(StringBuilder stringBuilder) => s_Pool.Release(stringBuilder);
        
        public static string ToStringRelease(StringBuilder stringBuilder)
        {
            var result = stringBuilder.ToString();
            s_Pool.Release(stringBuilder);
            return result;
        }
    }
}