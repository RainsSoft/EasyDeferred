using System;

namespace AssertHelper
{
    public static partial class Assert
    {
        #region IsGreaterThan
        public static void IsGreaterThan(decimal val, int comparer, string property, string message)
        {
            if ((double)val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(double val, int comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(float val, int comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(int val, int comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsGreaterOrEqualsThan
        public static void IsGreaterOrEqualsThan(decimal val, int comparer, string property, string message)
        {
            if ((double)val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(double val, int comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(float val, int comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(int val, int comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsLowerThan
        public static void IsLowerThan(decimal val, int comparer, string property, string message)
        {
            if ((double)val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(double val, int comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(float val, int comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(int val, int comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsLowerOrEqualsThan
        public static void IsLowerOrEqualsThan(decimal val, int comparer, string property, string message)
        {
            if ((double)val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(double val, int comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(float val, int comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(int val, int comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region AreEquals
        public static void AreEquals(decimal val, int comparer, string property, string message)
        {
            if ((double)val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(double val, int comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(float val, int comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(int val, int comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region AreNotEquals
        public static void AreNotEquals(decimal val, int comparer, string property, string message)
        {
            if ((double)val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(double val, int comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(float val, int comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(int val, int comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region Between      
        public static void IsBetween(int val, int from, int to, string property, string message)
        {
            if (!(val > from && val < to))
                throw new ArgumentException(message, property);

            
        }
        #endregion
    }
}
