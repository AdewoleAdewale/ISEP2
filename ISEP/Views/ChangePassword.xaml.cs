using Acr.UserDialogs;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ISEP.Services;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ChangePassword : ContentPage
    {
        public ChangePassword()
        {
            InitializeComponent();
        }

        private async void UpdatePassword_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OldPasswordEntry.Text) || string.IsNullOrWhiteSpace(ConfirmPassword.Text))
            {
                await DisplayAlert("NOTIFICATION", "Kindly fill in all fields before proceeding.", "TRY AGAIN");
                return;
            }

            if (LoginPage.Passwords != OldPasswordEntry.Text)
            {
                await DisplayAlert("NOTIFICATION", "Current password does not match.", "TRY AGAIN");
                return;
            }

            using (UserDialogs.Instance.Loading("Updating Password...", null, null, true))
            {
                string url = $"{BrandConfig.ApiBaseUrl}/api/taskpayers/SAChangePassword?UserName={LoginPage.ValidUserMail}&NewPassword={ConfirmPassword.Text.Trim()}";

                try
                {
                    var result = await ApiClient.GetAsync<InterfacePass>(url);

                    if (result != null && result.status == "00")
                    {
                        SessionService.ClearSession();
                        App.IsUserLoggedIn = false;
                        await DisplayAlert("NOTIFICATION", "Password Change Successful. Please log in again!", "OKAY");
                        Application.Current.MainPage = new NavigationPage(new LoginPage());
                    }
                    else
                    {
                        await DisplayAlert("NOTIFICATION", "Error: Password was not changed.", "OKAY");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("NOTIFICATION", "Network error. Please check your internet connection.", "TRY AGAIN");
                    System.Diagnostics.Debug.WriteLine($"Password update error: {ex.Message}");
                }
            }
        }

    }
}

public class InterfacePass
{
    public string MerchantSubUser { get; set; }

    public string status { get; set; }

    public string Password { get; set; }

    public string PhoneNumber { get; set; }

    public string FullName { get; set; }
}