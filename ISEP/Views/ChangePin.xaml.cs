using Acr.UserDialogs;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ISEP.Services;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ChangePin : ContentPage
    {
        public ChangePin()
        {
            InitializeComponent();
        }

        private async void UpdatePin_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OldPINEntry.Text) || string.IsNullOrWhiteSpace(ConfirmPIN.Text))
            {
                await DisplayAlert("NOTIFICATION", "Kindly fill in all fields before proceeding.", "TRY AGAIN");
                return;
            }

            if (OldPINEntry.Text != LoginPage.Pin)
            {
                await DisplayAlert("NOTIFICATION", "Cannot confirm your old PIN. Please try again.", "OKAY");
                return;
            }

            using (UserDialogs.Instance.Loading("Updating PIN...", null, null, true))
            {
                string url = $"{BrandConfig.ApiBaseUrl}/api/taskpayers/SAChangePin?UserName={LoginPage.ValidUserMail}&NewPin={ConfirmPIN.Text.Trim()}";

                try
                {
                    var result = await ApiClient.GetAsync<InterfacePass>(url);

                    if (result != null && result.status == "00")
                    {
                        SessionService.ClearSession();
                        App.IsUserLoggedIn = false;
                        await DisplayAlert("NOTIFICATION", "PIN Change Successful. Please log in again!", "OKAY");
                        Application.Current.MainPage = new NavigationPage(new LoginPage());
                    }
                    else
                    {
                        await DisplayAlert("NOTIFICATION", "Error: PIN was not changed.", "OKAY");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("NOTIFICATION", "Network error. Please check your internet connection.", "TRY AGAIN");
                    System.Diagnostics.Debug.WriteLine($"PIN update error: {ex.Message}");
                }
            }
        }
    }
}