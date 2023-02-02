using System;

namespace AssertHelper
{
    public static partial class Assert
    {
        #region IsGreaterThan
        public static void IsGreaterThan(decimal val, decimal comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(double val, decimal comparer, string property, string message)
        {
            if (val <= (double)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(float val, decimal comparer, string property, string message)
        {
            if (val <= (float)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(int val, decimal comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsGreaterOrEqualsThan
        public static void IsGreaterOrEqualsThan(decimal val, decimal comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(double val, decimal comparer, string property, string message)
        {
            if (val < (double)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(float val, decimal comparer, string property, string message)
        {
            if (val < (float)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(int val, decimal comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsLowerThan
        public static void IsLowerThan(decimal val, decimal comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(double val, decimal comparer, string property, string message)
        {
            if (val >= (double)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(float val, decimal comparer, string property, string message)
        {
            if (val >= (float)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(int val, decimal comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsLowerOrEqualsThan
        public static void IsLowerOrEqualsThan(decimal val, decimal comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(double val, decimal comparer, string property, string message)
        {
            if (val > (double)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(float val, decimal comparer, string property, string message)
        {
            if (val > (float)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(int val, decimal comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region AreEquals
        public static void AreEquals(decimal val, decimal comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(double val, decimal comparer, string property, string message)
        {
            if (val != (double)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(float val, decimal comparer, string property, string message)
        {
            if (val != (float)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(int val, decimal comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region AreNotEquals
        public static void AreNotEquals(decimal val, decimal comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(double val, decimal comparer, string property, string message)
        {
            if (val == (double)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(float val, decimal comparer, string property, string message)
        {
            if (val == (float)comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(int val, decimal comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region Between
        public static void IsBetween(decimal val, decimal from, decimal to, string property, string message)
        {
            if (!(val > from && val < to))
                throw new ArgumentException(message, property);

            
        }      
        #endregion
    }
}
