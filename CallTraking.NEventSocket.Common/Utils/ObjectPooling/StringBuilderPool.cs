using CallTraking.NEventSocket.Common.Utils.Extensions;
using System.Text;

namespace CallTraking.NEventSocket.Common.Utils.ObjectPooling
{
    internal static class StringBuilderPool
    {
        public static StringBuilder Allocate()
        {
            return SharedPools.BigDefault<StringBuilder>().AllocateAndClear();
        }

        public static void Free(StringBuilder builder)
        {
            SharedPools.BigDefault<StringBuilder>().ClearAndFree(builder);
        }

        public static string ReturnAndFree(StringBuilder builder)
        {
            return SharedPools.BigDefault<StringBuilder>().ReturnAndFree(builder);
        }
    }
}
