namespace Uhrzeitrechner
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(FlightDetailPage), typeof(FlightDetailPage));
        }
    }
}
