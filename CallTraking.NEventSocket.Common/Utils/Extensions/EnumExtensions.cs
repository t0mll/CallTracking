using System;
using System.Collections.Generic;
using System.Linq;

namespace CallTraking.NEventSocket.Common.Utils.Extensions
{
    /// <summary>
    /// Extension methods for Enums
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Gets the unique flags of a flag Enum
        /// </summary>
        public static IEnumerable<Enum> GetUniqueFlags(this Enum flags)
        {
            return Enum.GetValues(flags.GetType()).Cast<Enum>().Where(flags.HasFlag);
        }
    }
}
