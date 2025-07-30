using System;
using Unity.Collections;
using UnityEngine;

// For Convert.ToBase64String and FromBase64String

namespace Scriptable.Settings
{
    public static class ShortGuid
    {
        // Encodes a Guid into a shorter, URL-friendly Base64 string
        public static string Encode(Guid guid)
        {
            if (guid == Guid.Empty) return string.Empty;
            string base64Guid = Convert.ToBase64String(guid.ToByteArray());
            // Replace URL-unfriendly characters
            base64Guid = base64Guid.Replace('+', '-').Replace('/', '_');
            // Remove padding '='
            return base64Guid.Substring(0, 22);
        }
        
        public static FixedString32Bytes Encode32(Guid guid)
        {
            if (guid == Guid.Empty) return string.Empty;
            string base64Guid = Convert.ToBase64String(guid.ToByteArray());
            // Replace URL-unfriendly characters
            base64Guid = base64Guid.Replace('+', '-').Replace('/', '_');
            // Remove padding '='
            return base64Guid.Substring(0, 22);
        }

        public static Guid Decode(FixedString32Bytes encoded)
        {
            return Decode(encoded.ToString());
        }

        // Decodes the short string back into a Guid
        public static Guid Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length != 22)
            {
                // Handle invalid input gracefully - perhaps return Guid.Empty or throw
                Debug.LogWarning($"Invalid ShortGuid format: '{encoded}'");
                return Guid.Empty;
            }
            // Add back padding and replace URL-friendly characters
            string base64Guid = encoded.Replace('-', '+').Replace('_', '/') + "==";
            try
            {
                byte[] guidBytes = Convert.FromBase64String(base64Guid);
                return new Guid(guidBytes);
            }
            catch (FormatException ex)
            {
                Debug.LogError($"Failed to decode ShortGuid '{encoded}': {ex.Message}");
                return Guid.Empty;
            }
        }

        // Tries to decode, returning true on success
        public static bool TryParse(string encoded, out Guid guid)
        {
            guid = Guid.Empty;
            if (string.IsNullOrEmpty(encoded) || encoded.Length != 22)
            {
                return false;
            }
            string base64Guid = encoded.Replace('-', '+').Replace('_', '/') + "==";
            try
            {
                byte[] guidBytes = Convert.FromBase64String(base64Guid);
                guid = new Guid(guidBytes);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}