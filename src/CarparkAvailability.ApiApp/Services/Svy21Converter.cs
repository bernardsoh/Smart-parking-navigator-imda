namespace CarparkAvailability.ApiApp.Services;

public static class Svy21Converter
{
    public static (double Latitude, double Longitude) Convert(double easting, double northing)
    {
        double a = 6378137;
        double f = 1.0 / 298.257223563;
        double e2 = 2 * f - f * f;

        double k0 = 1;
        double e0 = 28001.642;
        double n0 = 38744.572;
        double lon0 = 103.8333333333333 * Math.PI / 180;
        double lat0 = 1.3666666666667 * Math.PI / 180;

        double n = northing - n0;
        double e = easting - e0;

        double m0 = MeridionalArc(lat0, a, e2);
        double m = m0 + n / k0;

        double mu = m / (a * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256));
        double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));

        double phi1 = mu
            + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu)
            + (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu)
            + (151 * Math.Pow(e1, 3) / 96) * Math.Sin(6 * mu)
            + (1097 * Math.Pow(e1, 4) / 512) * Math.Sin(8 * mu);

        double sinPhi1 = Math.Sin(phi1);
        double cosPhi1 = Math.Cos(phi1);
        double tanPhi1 = Math.Tan(phi1);

        double n1 = a / Math.Sqrt(1 - e2 * sinPhi1 * sinPhi1);
        double t1 = tanPhi1 * tanPhi1;
        double c1 = (e2 / (1 - e2)) * cosPhi1 * cosPhi1;
        double r1 = a * (1 - e2) / Math.Pow(1 - e2 * sinPhi1 * sinPhi1, 1.5);
        double d = e / (n1 * k0);

        double lat = phi1
            - (n1 * tanPhi1 / r1) * (
                d * d / 2
                - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * e2 / (1 - e2)) * Math.Pow(d, 4) / 24
                + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * e2 / (1 - e2) - 3 * c1 * c1) * Math.Pow(d, 6) / 720);

        double lon = lon0 + (
            d
            - (1 + 2 * t1 + c1) * Math.Pow(d, 3) / 6
            + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * e2 / (1 - e2) + 24 * t1 * t1) * Math.Pow(d, 5) / 120) / cosPhi1;

        return (lat * 180 / Math.PI, lon * 180 / Math.PI);
    }

    public static double DistanceMetres(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMetres = 6371000;
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                   + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                   * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return earthRadiusMetres * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static double MeridionalArc(double latitude, double a, double e2)
    {
        return a * (
            (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256) * latitude
            - (3 * e2 / 8 + 3 * e2 * e2 / 32 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(2 * latitude)
            + (15 * e2 * e2 / 256 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(4 * latitude)
            - (35 * e2 * e2 * e2 / 3072) * Math.Sin(6 * latitude));
    }
}
