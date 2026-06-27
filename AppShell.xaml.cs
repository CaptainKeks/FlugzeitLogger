namespace Uhrzeitrechner
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(FlightDetailPage), typeof(FlightDetailPage));

            // Beim Start direkt auf der Flugseite landen
            Dispatcher.Dispatch(async () => await GoToAsync("//FlightPage"));
        }
    }
}
