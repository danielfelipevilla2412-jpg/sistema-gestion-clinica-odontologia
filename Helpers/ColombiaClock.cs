namespace SmileTrack_MVC.Helpers;

public static class ColombiaClock
{
    private static readonly TimeZoneInfo ColombiaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    public static DateTime Now =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ColombiaTimeZone);

    public static DateTime Today => Now.Date;
}
