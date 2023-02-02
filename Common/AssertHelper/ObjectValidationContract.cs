using System;

namespace AssertHelper
{
    public static partial class Assert
    {
        public static void IsNull(object obj, string property, string message)
        {
            if (obj != null)
                throw new ArgumentException(message, property);

            
        }

        public static void IsNotNull(object obj, string property, string message)
        {
            if (obj == null)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(object obj, object comparer, string property, string message)
        {
            if (obj != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(object obj, object comparer, string property, string message)
        {
            if (obj == comparer)
                throw new ArgumentException(message, property);

            
        }
    }
}