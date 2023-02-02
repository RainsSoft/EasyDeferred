using System;

namespace AssertHelper
{
    public static partial class Assert
    {
        public static void AreEquals(Guid val, Guid comparer, string property, string message)
        {
            // TODO: StringComparison.OrdinalIgnoreCase not suported yet
            if (val.ToString() != comparer.ToString())
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(Guid val, Guid comparer, string property, string message)
        {
            // TODO: StringComparison.OrdinalIgnoreCase not suported yet
            if (val.ToString() == comparer.ToString())
                throw new ArgumentException(message, property);

            
        }

        public static void IsEmpty(Guid val, string property, string message)
        {
            if (val != Guid.Empty)
                throw new ArgumentException(message, property);

            
        }

        public static void IsNotEmpty(Guid val, string property, string message)
        {
            if (val == Guid.Empty)
                throw new ArgumentException(message, property);

            
        }
    }
}