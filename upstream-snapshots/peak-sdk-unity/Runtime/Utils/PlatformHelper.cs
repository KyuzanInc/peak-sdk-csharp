using UnityEngine;

namespace Peak.Utils
{
    public static class PlatformHelper
    {
        public static bool IsMobilePlatform()
        {
#if UNITY_IOS || UNITY_ANDROID
            return true;
#else
            return Application.isMobilePlatform;
#endif
        }

        public static string GetPlatformName()
        {
#if UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#else
            return Application.platform.ToString();
#endif
        }

        public static bool IsIOSPlatform()
        {
#if UNITY_IOS
            return true;
#else
            return Application.platform == RuntimePlatform.IPhonePlayer;
#endif
        }

        public static bool IsAndroidPlatform()
        {
#if UNITY_ANDROID
            return true;
#else
            return Application.platform == RuntimePlatform.Android;
#endif
        }
    }
}