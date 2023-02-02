using System;

namespace AssertHelper
{
    public static partial class Assert
    {
        public static void IsGreaterThan(DateTime val, DateTime comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(DateTime val, DateTime comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(DateTime val, DateTime comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(DateTime val, DateTime comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsBetween(DateTime val, DateTime from, DateTime to, string property, string message)
        {
            if (!(val > from && val < to))
                throw new ArgumentException(message, property);

            
        }

    }
}
