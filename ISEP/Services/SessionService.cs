using Xamarin.Essentials;
using System;
using Newtonsoft.Json;
using ISEP.Views;

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
            bool hasToken = !string.IsNullOrEmpty(token);
            if (hasToken)
            {
                RestoreSession();
            }
            return hasToken;
        }

        /// <summary>
        /// Restores static session fields from saved UserDataJson in Preferences if static state is lost.
        /// </summary>
        public static bool RestoreSession()
        {
            try
            {
                string json = Preferences.Get(KeyUserData, string.Empty);
                string savedEmail = SavedEmail;

                if (!string.IsNullOrEmpty(json))
                {
                    var response = JsonConvert.DeserializeObject<LoginResponse>(json);
                    if (response?.detail != null)
                    {
                        var d = response.detail;
                        LoginPage.ValidUserMail = string.IsNullOrEmpty(savedEmail) ? d.email : savedEmail;
                        LoginPage.Passwords = d.password;
                        LoginPage.Name = d.name;
                        LoginPage.Token = d.token;
                        LoginPage.Pin = d.pin;
                        LoginPage.Super_Agent = d.SuperAgent;
                        LoginPage.accountbalance = d.account_Balance;
                        LoginPage.tradingstatus = d.tradingStatus;
                        LoginPage.accountnumbers = d.accountNumber;
                        LoginPage.Banks = d.bank;
                        return true;
                    }
                }

                if (!string.IsNullOrEmpty(savedEmail))
                {
                    LoginPage.ValidUserMail = savedEmail;
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionService] RestoreSession error: {ex}");
            }
            return false;
        }

        /// <summary>
        /// Ensures static properties on LoginPage are hydrated.
        /// Call this in OnAppearing across views.
        /// </summary>
        public static void EnsureSessionRestored()
        {
            if (string.IsNullOrEmpty(LoginPage.ValidUserMail) || string.IsNullOrEmpty(LoginPage.accountbalance))
            {
                RestoreSession();
            }
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