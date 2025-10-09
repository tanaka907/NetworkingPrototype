using System.Text;
using UnityEngine;
using UnityEngine.Pool;

namespace NetworkingPrototype
{
    public static class StringBuilderPool
    {
        private static ObjectPool<StringBuilder> s_pool;

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            Application.quitting += OnQuit;
            s_pool = new ObjectPool<StringBuilder>(() => new StringBuilder(), null, sb => sb.Clear());
        }

        private static void OnQuit()
        {
            s_pool = null;
        }

        public static StringBuilder Get() => s_pool.Get();
        
        public static void Release(StringBuilder stringBuilder) => s_pool.Release(stringBuilder);
        
        public static string ToStringRelease(StringBuilder stringBuilder)
        {
            var result = stringBuilder.ToString();
            s_pool.Release(stringBuilder);
            return result;
        }
    }
}