using System;

namespace AssertHelper
{
    public static partial class Assert
    {
        #region IsGreaterThan
        public static void IsGreaterThan(decimal val, float comparer, string property, string message)
        {
            if ((double)val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(double val, float comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(float val, float comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(int val, float comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsGreaterOrEqualsThan
        public static void IsGreaterOrEqualsThan(decimal val, float comparer, string property, string message)
        {
            if ((double)val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(double val, float comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(float val, float comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(int val, float comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsLowerThan
        public static void IsLowerThan(decimal val, float comparer, string property, string message)
        {
            if ((double)val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(double val, float comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(float val, float comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(int val, float comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsLowerOrEqualsThan
        public static void IsLowerOrEqualsThan(decimal val, float comparer, string property, string message)
        {
            if ((double)val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(double val, float comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(float val, float comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(int val, float comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region AreEquals
        public static void AreEquals(decimal val, float comparer, string property, string message)
        {
            if ((double)val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(double val, float comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(float val, float comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(int val, float comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region AreNotEquals
        public static void AreNotEquals(decimal val, float comparer, string property, string message)
        {
            if ((double)val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(double val, float comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(float val, float comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(int val, float comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region Between      
        public static void IsBetween(float val, float from, float to, string property, string message)
        {
            if (!(val > from && val < to))
                throw new ArgumentException(message, property);

            
        }
        #endregion
    }
}
