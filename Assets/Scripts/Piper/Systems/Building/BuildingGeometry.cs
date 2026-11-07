using System;
using System.Collections.Generic;

namespace PipeWorld.Piper
{
    // Footprint-plane math. Points are (x, z); the names follow the builder-proto geometry module.
    public static class BuildingGeometry
    {
        public static double dist(double ax, double az, double bx, double bz)
        {
            double dx = ax - bx, dz = az - bz;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public static double distPointToSegment(double px, double pz, double ax, double az, double bx, double bz)
        {
            closestPointOnSeg(px, pz, ax, az, bx, bz, out double cx, out double cz, out _);
            return dist(px, pz, cx, cz);
        }

        public static void closestPointOnSeg(double px, double pz, double ax, double az, double bx, double bz, out double cx, out double cz, out double t)
        {
            double dx = bx - ax, dz = bz - az;
            double lenSq = dx * dx + dz * dz;
            if (lenSq == 0)
            {
                cx = ax; cz = az; t = 0;
                return;
            }
            t = ((px - ax) * dx + (pz - az) * dz) / lenSq;
            t = Math.Max(0, Math.Min(1, t));
            cx = ax + t * dx;
            cz = az + t * dz;
        }

        // Distance along the ray at which it crosses the segment, or null. Hits at the very ends of the segment are ignored.
        public static double? raySegIntersect(double ox, double oz, double dx, double dz, double ax, double az, double bx, double bz)
        {
            double ex = bx - ax, ez = bz - az;
            double denom = dx * ez - dz * ex;
            if (Math.Abs(denom) < 1e-10) return null;
            double t = ((ax - ox) * ez - (az - oz) * ex) / denom;
            double u = ((ax - ox) * dz - (az - oz) * dx) / denom;
            if (t > 1e-6 && u >= 0.001 && u <= 0.999) return t;
            return null;
        }

        private static double cross(double ux, double uz, double vx, double vz) => ux * vz - uz * vx;

        // Proper crossings only: touching at endpoints and collinear overlaps are not intersections.
        public static bool segmentsIntersect(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            double abx = bx - ax, abz = bz - az;
            double d1 = cross(abx, abz, cx - ax, cz - az);
            double d2 = cross(abx, abz, dx - ax, dz - az);
            if (d1 * d2 > 0) return false;
            double cdx = dx - cx, cdz = dz - cz;
            double d3 = cross(cdx, cdz, ax - cx, az - cz);
            double d4 = cross(cdx, cdz, bx - cx, bz - cz);
            if (d3 * d4 > 0) return false;
            if (d1 == 0 && d2 == 0) return false;
            if (d1 * d2 == 0 || d3 * d4 == 0) return false;
            return true;
        }

        public static bool pointInPolygon(double px, double pz, IList<(double x, double z)> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                double xi = poly[i].x, zi = poly[i].z, xj = poly[j].x, zj = poly[j].z;
                if ((zi > pz) != (zj > pz) && px < (xj - xi) * (pz - zi) / (zj - zi) + xi)
                    inside = !inside;
            }
            return inside;
        }

        public static bool segmentsOverlapCollinear(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            double abx = bx - ax, abz = bz - az;
            double cross1 = abx * (cz - az) - abz * (cx - ax);
            double cross2 = abx * (dz - az) - abz * (dx - ax);
            if (Math.Abs(cross1) > 1e-6 || Math.Abs(cross2) > 1e-6) return false;

            bool useX = Math.Abs(abx) >= Math.Abs(abz);
            double a1 = useX ? ax : az, b1 = useX ? bx : bz, c1 = useX ? cx : cz, d1 = useX ? dx : dz;
            double lo1 = Math.Min(a1, b1), hi1 = Math.Max(a1, b1);
            double lo2 = Math.Min(c1, d1), hi2 = Math.Max(c1, d1);
            return Math.Min(hi1, hi2) - Math.Max(lo1, lo2) > 1e-6;
        }

        public static double signedPolyArea(IList<(double x, double z)> verts)
        {
            double s = 0;
            for (int i = 0; i < verts.Count; i++)
            {
                int j = (i + 1) % verts.Count;
                s += verts[i].x * verts[j].z - verts[j].x * verts[i].z;
            }
            return s / 2;
        }
    }
}
