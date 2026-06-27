namespace Uhrzeitrechner.Services;

public static class AppPaths
{
    public static string FlightLogPath =>
        Path.Combine(FileSystem.AppDataDirectory, "flights.json");

    public static string SessionPath =>
        Path.Combine(FileSystem.AppDataDirectory, "session.json");
}
