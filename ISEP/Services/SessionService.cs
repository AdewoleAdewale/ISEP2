using Xamarin.Essentials;
using System;

namespace ISEP.Services
{
    public static class SessionService
    {
        private const string KeyRememberMe = "RememberMe";
        private const string KeyRememberPassword = "RememberPassword";
        private const string KeyEmail = "SavedEmail";
        private const string KeyPassword = "SavedPassword";
        private const string KeyToken = "AuthToken";
        private const string KeyUserData = "UserDataJson";

        public static bool IsRememberMe
        {
            get => Preferences.Get(KeyRememberMe, false);
            set => Preferences.Set(KeyRememberMe, value);
        }

        public static bool IsRememberPassword
        {
            get => Preferences.Get(KeyRememberPassword, false);
            set => Preferences.Set(KeyRememberPassword, value);
        }

        public static string SavedEmail
        {
            get => Preferences.Get(KeyEmail, string.Empty);
            set => Preferences.Set(KeyEmail, value);
        }

        public static string SavedPassword
        {
            get => Preferences.Get(KeyPassword, string.Empty);
            set => Preferences.Set(KeyPassword, value);
        }

        public static void SaveSession(string email, string password, string token, string userDataJson)
        {
            SavedEmail = email;
            Preferences.Set(KeyToken, token);
            Preferences.Set(KeyUserData, userDataJson);

            if (IsRememberPassword)
                SavedPassword = password;
            else
                Preferences.Remove(KeyPassword);
        }

        public static bool TryAutoLogin()
        {
            if (!IsRememberMe) return false;
            var token = Preferences.Get(KeyToken, string.Empty);
            return !string.IsNullOrEmpty(token);
        }

        public static void ClearSession()
        {
            Preferences.Remove(KeyToken);
            Preferences.Remove(KeyUserData);
            if (!IsRememberPassword)
            {
                Preferences.Remove(KeyPassword);
                Preferences.Remove(KeyEmail);
            }
        }
    }
}