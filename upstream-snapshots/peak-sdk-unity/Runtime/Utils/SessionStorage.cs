using System;
using UnityEngine;

namespace Peak.Utils
{
    /// <summary>
    /// Stores and retrieves authenticated session metadata using PlayerPrefs.
    /// </summary>
    public static class SessionStorage
    {
        private const string SessionKey = "peak-sdk-unity:auth-session";

        [Serializable]
        public class SessionData
        {
            public string email;
            public string sessionJwt;
            public string targetPrivateKey;
            public string targetPublicKey;
            public long savedAt;
        }

        public static void Save(SessionData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[SessionStorage] Attempted to save null session data.");
                return;
            }

            try
            {
                var json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(SessionKey, json);
                PlayerPrefs.Save();
                Debug.Log("[SessionStorage] Session stored successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionStorage] Failed to save session: {ex.Message}");
            }
        }

        public static SessionData Load()
        {
            if (!PlayerPrefs.HasKey(SessionKey))
            {
                return null;
            }

            try
            {
                var json = PlayerPrefs.GetString(SessionKey);
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }

                return JsonUtility.FromJson<SessionData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionStorage] Failed to load session: {ex.Message}");
                return null;
            }
        }

        public static void Clear()
        {
            if (PlayerPrefs.HasKey(SessionKey))
            {
                PlayerPrefs.DeleteKey(SessionKey);
                PlayerPrefs.Save();
                Debug.Log("[SessionStorage] Session cleared.");
            }
        }

        public static bool HasSession()
        {
            return PlayerPrefs.HasKey(SessionKey);
        }
    }
}
