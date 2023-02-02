﻿using System;

namespace AssertHelper
{
    public static partial class Assert
    {
        #region IsGreaterThan
        public static void IsGreaterThan(decimal val, double comparer, string property, string message)
        {
            if ((double)val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(double val, double comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(float val, double comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterThan(int val, double comparer, string property, string message)
        {
            if (val <= comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsGreaterOrEqualsThan
        public static void IsGreaterOrEqualsThan(decimal val, double comparer, string property, string message)
        {
            if ((double)val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(double val, double comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(float val, double comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsGreaterOrEqualsThan(int val, double comparer, string property, string message)
        {
            if (val < comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsLowerThan
        public static void IsLowerThan(decimal val, double comparer, string property, string message)
        {
            if ((double)val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(double val, double comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(float val, double comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerThan(int val, double comparer, string property, string message)
        {
            if (val >= comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region IsLowerOrEqualsThan
        public static void IsLowerOrEqualsThan(decimal val, double comparer, string property, string message)
        {
            if ((double)val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(double val, double comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(float val, double comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void IsLowerOrEqualsThan(int val, double comparer, string property, string message)
        {
            if (val > comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region AreEquals
        public static void AreEquals(decimal val, double comparer, string property, string message)
        {
            if ((double)val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(double val, double comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(float val, double comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(int val, double comparer, string property, string message)
        {
            if (val != comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region AreNotEquals
        public static void AreNotEquals(decimal val, double comparer, string property, string message)
        {
            if ((double)val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(double val, double comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(float val, double comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(int val, double comparer, string property, string message)
        {
            if (val == comparer)
                throw new ArgumentException(message, property);

            
        }
        #endregion

        #region Between     
        public static void IsBetween(double val, double from, double to, string property, string message)
        {
            if (!(val > from && val < to))
                throw new ArgumentException(message, property);

            
        }        
        #endregion
    }
}
