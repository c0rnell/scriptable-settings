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

        public static bool TryDecode(FixedString32Bytes encoded, out Guid guid)
        {
            return TryDecode(encoded.ToString(), out guid);
        }

        // Decodes the short string back into a Guid
        public static bool TryDecode(string encoded, out Guid guid)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length != 22)
            {
                guid = Guid.Empty;
                return false;
            }
            // Add back padding and replace URL-friendly characters
            string base64Guid = encoded.Replace('-', '+').Replace('_', '/') + "==";
            try
            {
                byte[] guidBytes = Convert.FromBase64String(base64Guid);
                guid = new Guid(guidBytes);
                return true;
            }
            catch (FormatException ex)
            {
                Debug.LogError($"Failed to decode ShortGuid '{encoded}': {ex.Message}");
                guid = Guid.Empty;
                return false;
            }
        }
    }
}