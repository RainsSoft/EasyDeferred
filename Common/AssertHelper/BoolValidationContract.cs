using System;

namespace AssertHelper
{
    public static partial class Assert
    {
        public static void IsTrue(bool val, string property, string message)
        {
            if (!val)
                throw new ArgumentException(message, property);

            
        }

        public static void IsFalse(bool val, string property, string message)
        {
            if (val)
                throw new ArgumentException(message, property);

            
        }
    }
}