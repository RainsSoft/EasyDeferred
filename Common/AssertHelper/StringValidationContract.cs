using System;
using System.Text.RegularExpressions;

namespace AssertHelper
{
    public static partial class Assert
    {
        public static void IsNotNullOrEmpty(string val, string property, string message)
        {
            if (string.IsNullOrEmpty(val))
                throw new ArgumentException(message, property); 
        }

        public static void IsNullOrEmpty(string val, string property, string message)
        {
            if (!string.IsNullOrEmpty(val))
                throw new ArgumentException(message, property);

            
        }

        public static void HasMinLen(string val, int min, string property, string message)
        {
            if (string.IsNullOrEmpty(val) || val.Length < min)
                throw new ArgumentException(message, property);

            
        }

        public static void HasMaxLen(string val, int max, string property, string message)
        {
            if (string.IsNullOrEmpty(val) || val.Length > max) 
                throw new ArgumentException(message, property);

            
        }

        public static void HasLen(string val, int len, string property, string message)
        {
            if (string.IsNullOrEmpty(val) || val.Length != len)
                throw new ArgumentException(message, property);

            
        }

        public static void Contains(string val, string text, string property, string message)
        {
            // TODO: StringComparison.OrdinalIgnoreCase not suported yet
            if (!val.Contains(text))
                throw new ArgumentException(message, property);

            
        }

        public static void AreEquals(string val, string text, string property, string message)
        {
            // TODO: StringComparison.OrdinalIgnoreCase not suported yet
            if (val != text)
                throw new ArgumentException(message, property);

            
        }

        public static void AreNotEquals(string val, string text, string property, string message)
        {
            // TODO: StringComparison.OrdinalIgnoreCase not suported yet
            if (val == text)
                throw new ArgumentException(message, property);

            
        }

        public static void IsEmail(string email, string property, string message)
        {
            const string pattern = @"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$";
                Matchs(email, pattern, property, message);
        }

        public static void IsEmailOrEmpty(string email, string property, string message)
        {
            if (string.IsNullOrEmpty(email))
             IsEmail(email, property, message);
        }

        public static void IsUrl(string url, string property, string message)
        {
            const string pattern = @"^(http:\/\/www\.|https:\/\/www\.|http:\/\/|https:\/\/)[a-z0-9]+([\-\.]{1}[a-z0-9]+)*\.[a-z]{2,5}(:[0-9]{1,5})?(\/.*)?$";
                Matchs(url, pattern, property, message);
        }

        public static void IsUrlOrEmpty(string url, string property, string message)
        {
            if (string.IsNullOrEmpty(url))
                IsUrl(url, property, message);
        }

        public static void Matchs(string text, string pattern, string property, string message)
        {
            if (!Regex.IsMatch(text ?? "", pattern))
                throw new ArgumentException(message, property);
        }
    }
}
