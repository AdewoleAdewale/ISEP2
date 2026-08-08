using ISEP.ViewModels;
using Xamarin.Forms;

namespace ISEP
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            BindingContext = new OnboardingPageViewModel();
        }

        private void TapGestureRecognizer_Tapped(object sender, System.EventArgs e)
        {
            Navigation.PushModalAsync(new Views.LoginPage());
        }
    }
}
