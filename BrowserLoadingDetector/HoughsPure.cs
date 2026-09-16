using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using MoreLinq;
 using System;
 using System.Collections.Generic;
 using System.IO;
 using System.Linq;
 using System.Runtime.InteropServices;

 
namespace BrowserLoadingDetector
{
    internal static class MathCompability
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int value, int min, int max)
        {
            if (min > max)
            {
                throw new ArgumentException($"Aufruf mit min > max: {min} > {max}.");
            }

            if (value < min)
            {
                return min;
            }
            else if (value > max)
            {
                return max;
            }

            return value;
        }

        public static double[] Sequence(double start,  double stopInclusive,double step)
        {
            return MoreEnumerable
                   .Generate(start, x => x + step)
                   .TakeWhile(x => x <= stopInclusive)
                   .ToArray();
        }
    }


    /// <summary>
    /// Helper class for Hough-Line-Transform for computing the votes for a specific given line.
    /// 
    /// Usage: Construct an instance, then call CountHits on it.
    ///
    /// The instance computes a lookup table, so reusing it is recommended.
    /// </summary>
    internal class CountPointsLine
    {
        private readonly double[] lookupSin;
        private readonly double[] lookupTan;

        public CountPointsLine(double angleResolution, double thickness)
        {
            AngleResolution = angleResolution;
            Thickness       = thickness;

            double[] angles = MathCompability.Sequence(0.0, Math.PI, angleResolution);

            lookupSin = angles.Select(angle => Math.Sin(Math.PI / 2 - angle)).ToArray();
            lookupTan = angles.Select(angle => Math.Tan(angle)).ToArray();
        }

        public double AngleResolution { get; }
        public double Thickness       { get; }

        private double Sin(double x)
        {
            double angle = -(x - Math.PI / 2);
            int    index = (int)(angle   / AngleResolution + 0.0001);
            return lookupSin[index];
        }

        private double Tan(double x)
        {
            double angle = x;
            int    index = (int)(angle / AngleResolution + 0.001);
            return lookupTan[index];
        }

        /// <summary>
        ///     Given a boolean-like matrix (i.e. any value >0 is seens as a hit),
        ///     this method draws a line, which goes through the given point
        ///     and has, with regard to the horizontal line, the angle in radians and Thickness in pixel,
        ///     with the latter as given by the instance properties .
        ///     Then tests for each point on the drawn line, if the corresponding cell in the matrix is True, and if so, adds one
        ///     hit.
        /// </summary>
        /// <param name="image">A boolean-like matrix</param>
        /// <param name="point">A 2D-Point. Can be fractional</param>
        /// <param name="angle">
        ///     An angle in radian, where the angles are measured such that the line (0,0)-&gt;(x,x) for $x \neq 0$
        ///     has an angle of 45°
        /// </param>
        /// <returns></returns>
        public int CountHits(IBWImage image, (double x, double y) point, double angle)
        {
            int hits = 0;


            /*
             * Für ImageSharp gilt: Ein Punkt (x,y) entspricht in image dem Index [x,y] (anders als in Numpy, wo es umgekehrt ist).
             * Weiterhin korrespondiert image.Width mit x.
             */
            for (int x = 0; x < image.Width; x++)
            {
                // If y is almost infinite, (i.e. tan is near its polar point), we don't even need to bother
                if (Math.Abs(angle - Math.PI / 2) < 1E-10 && x - point.x != 0) continue;
                double y = (x - point.x) * Tan(angle) + point.y;


                double thicknessVertical;
                if (Math.Abs(Math.PI / 2 - angle) < 1E-10)
                    thicknessVertical = image.Height;
                else
                    thicknessVertical = Thickness / Math.Abs(Sin(Math.PI / 2 - angle));

                int lowerBoundInc   = Math.Max(1 + (int)(y   - thicknessVertical), 0);
                int higherBoundExcl = 1 + MathCompability.Clamp((int)(y + thicknessVertical), 0, image.Height - 1);


                for (int i = lowerBoundInc; i < higherBoundExcl; i++) hits += image[x, i] > 0 ? 1 : 0;
            }

            return hits;
        }
        
        public static void RemoveLine(IBWImage image, (double x, double y) point, double angle,double thickness)
        {


            /*
             * Für ImageSharp gilt: Ein Punkt (x,y) entspricht in image dem Index [x,y] (anders als in Numpy, wo es umgekehrt ist).
             * Weiterhin korrespondiert image.Width mit x.
             */
            for (int x = 0; x < image.Width; x++)
            {
                // If y is almost infinite, (i.e. tan is near its polar point), we don't even need to bother
                if (Math.Abs(angle - Math.PI / 2) < 1E-10 && x - point.x != 0) continue;
                double y = (x - point.x) * Math.Tan(angle) + point.y;


                double thicknessVertical;
                if (Math.Abs(Math.PI / 2 - angle) < 1E-10)
                    thicknessVertical = image.Height;
                else
                    thicknessVertical = thickness / Math.Abs(Math.Sin(Math.PI / 2 - angle));

                int lowerBoundInc   = Math.Max(1 + (int)(y              - thicknessVertical), 0);
                int higherBoundExcl = 1 + MathCompability.Clamp((int)(y + thicknessVertical), 0, image.Width - 1);


                for (int i = lowerBoundInc; i < higherBoundExcl; i++) image[x, i]=0;
            }

        }
    }

    /// <summary>
    /// Helper class for Hough-Circle-Transform for computing the votes for a specific given circle. Use the method CountHits.
    ///
    /// Warning: This class's method isn't optimized at all and uses raw brute force, so it won't scale to bigger pictures (28x28 works in a few ms still).
    /// </summary>
    internal static class CountPointsCircle
    {
        /// <summary>
        ///     Given a boolean-like matrix (i.e. any value >0 is seens as a hit),
        ///     this method draws a circle, which has the given point as center and the given radius
        /// 
        ///      Then tests for each point on the drawn circle
        ///      if the corresponding cell in the matrix is True, and if so, adds one hit.
        ///
        ///      Warning: This method's class isn't optimized at all and uses raw brute force, so it won't scale to bigger pictures (28x28 works in a few ms still).
        /// </summary>
        /// <param name="image">A boolean-like matrix</param>
        /// <param name="point">A 2D-Point. Can be fractional</param>
        /// <param name="radius">
        ///     Radius of the circle to draw, in pixel.
        /// </param>
        /// <param name="thickness">The thickness of the circle outline, in pixel.</param>
        /// <returns></returns>
        public static int CountHits(IBWImage image, (double x, double y) point, double radius, double thickness)
        {
            int hits = 0;


            /*
             * Für ImageSharp gilt: Ein Punkt (x,y) entspricht in image dem Index [x,y] (anders als in Numpy, wo es umgekehrt ist).
             * Weiterhin korrespondiert image.Width mit x.
             */
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    double distance = Math.Sqrt((x - point.x) * (x - point.x) + (y - point.y) * (y - point.y));
                    if (distance - thickness <= radius && radius <= distance + thickness)
                        hits += image[x, y] > 0 ? 1 : 0;
                }
            }

            return hits;
        }
    }

    /// <summary>
    /// Adapter interface for any matrix-like object that can be used as a grayscale image (even if itself is color).
    ///
    /// It is advised to implement for each implementation both explicit conversions between the matrix class and the adapter class.
    /// This way, one can use (IBWImage) mat to somewhat comfortably use the methods.
    /// </summary>
    public interface IBWImage
    {
        /// <summary>
        ///  Number of pixels along the horizontal axis (x-axis) in the image
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Number of pixels along the vertical axis (y-axis) in the image
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gives the maximum coordinates (x,y) which are still within the rectangle. Deliberately not named Shape, which
        /// could be missunderstood as rows x cols
        /// </summary>
        (int x, int y) Size { get; }

        /// <summary>
        /// For the pixel at position (x,y): 1 if the pixel is white, 0 otherwise
        /// Note: Match the coordinates such that (x+Δ,y) for increasing/decreasing Δ makes the point move along the x-axis (horizontally)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        byte this[int x, int y] { get; set; }

        // GetXAxisSlice und GetYAxisSlice sind interessant, aber welcher der beiden cache-freundlich ist, ist von Implementierung
        // abhängig, und kann nicht verändert werden, ohne die Matrix zu kopieren...
        // Das richtige wäre also ein GetAxisSlice, dass die Reihe/Spalte zurückgibt, und sagt, ob es Reihe oder Spalte ist
        // bzw. ein anderes Property sagt dies
    }

    public static class HoughTransform
    {
        /// <summary>
        /// Computes the hough-line transform for the given picture
        /// (grey-scale, pixels with value 0 are seen as empty, and >0 as filled pixel)
        /// , but only for the lines along the vertical axis
        /// </summary>
        /// <param name="image">See IBWImage.</param>
        /// <param name="angleResolution">Starting from 0° to 180°, angleResolution gives the step size of the angles to test</param>
        /// <param name="lineThickness"></param>
        /// <returns>An array such that each entry tells you:
        /// For each geometric point (0,y_coord), for each line going through this point with angle_radians, how many points does the line hit?</returns>
        public static (int y_coord, double angle_radians, int hits)[] HoughLinesWithXEquals0
            (IBWImage image, double angleResolution, float lineThickness)
        {
            double[] angles = MathCompability.Sequence(0, Math.PI, angleResolution);
            var                 cp     = new CountPointsLine(angleResolution, lineThickness);
            (int y_coord, double angle_radians, int hits)[]
                results = new (int, double, int)[image.Height * angles.Length];
            int curRow = -1;
            for (int y = 0; y < image.Height; y++)
            {
                foreach (double angle in angles)
                {
                    curRow += 1;
                    results[curRow] = (y,
                                       angle,
                                       cp.CountHits(image, (0, y), angle)
                        );
                }
            }

            return results;
        }
        /// <summary>
        /// Computes the hough-circle transform for the given picture
        /// (grey-scale, pixels with value 0 are seen as empty, and >0 as filled pixel)
        ///
        /// 
        /// Warning: This method's class isn't optimized at all and uses raw brute force, so it won't scale to bigger pictures (28x28 works in a few ms still).
        /// </summary>
        /// <param name="matrix">See IBWImage.</param>
        /// <param name="circleThickness">The thickness of the circles to detect. Stay conservative, as thick lines may make it easy to match anything</param>
        /// <returns>A list such that each entry tells you:
        /// For each circle with center at the geometric point (x,y) and radius 'radius',
        /// how many points does this circle hit?</returns>
        public static List<((int x, int y), double radius, int hits)> HoughCircles
            (
            IBWImage matrix,
            float      circleThickness
            )
        {
            List<((int x, int y), double radius, int hits)>
                results = new List<((int x, int y), double radius, int hits)>();
            int curRow = -1;
            for (int x = 0; x < matrix.Width; x++)
            {
                for (int y = 0; y < matrix.Height; y++)
                {
                    for (int radius = 0; radius < Math.Max(matrix.Width, matrix.Height); radius++)
                    {
                        curRow += 1;
                        results.Add(((x, y),
                                     radius,
                                     CountPointsCircle.CountHits(matrix, (x, y), radius, circleThickness))
                                   );
                    }
                }
            }

            return results;
        }
    }
    public class BitmapMat: IBWImage
    {
        private readonly byte[,] _grayByteArray;
        
        /// <summary>
        /// Is implicitly converted to a thresholded black-white bitmap.
        /// Everything >=128 is white (pixel value 1), everything else is black (pixel value 0)
        /// </summary>
        /// <param name="bitmap"></param>
        public BitmapMat(Bitmap bitmap)
        {
            _grayByteArray = ToGrayArray(bitmap);
            // Thresholding
            for(int i=0; i < _grayByteArray.GetLength(0); i++)
                for (int j = 0; j < _grayByteArray.GetLength(1); j++)
                    _grayByteArray[i, j] = (byte)(_grayByteArray[i, j] > 127 ? 1 : 0);      // In ASM wird das korrekt zu einer Shift-Anweisung? ist nämlich äq zu  _grayByteArray[i, j]>> 7
            
            
            Width          = _grayByteArray.GetLength(1);
            Height         = _grayByteArray.GetLength(0);
            Size           = (Width, Height);
        }
        
        /// <summary>
        /// Saves the passed byte-array as is
        /// </summary>
        /// <param name="byte2D"></param>
        public BitmapMat(byte[,] byte2D)
        {
            _grayByteArray = byte2D;
            Width          = _grayByteArray.GetLength(0);
            Height         = _grayByteArray.GetLength(1);
            Size           = (Width, Height);
        }
        public int            Width  { get; }
        public int            Height { get; }
        public (int x, int y) Size   { get; }

        public byte this[int x, int y]
        {
            get => _grayByteArray[y, x];
            set => _grayByteArray[y,x] =value;
        }

        // public static explicit operator Bitmap(BitmapMat d) => d._grayByteArray;
        public static explicit operator BitmapMat(Bitmap d) => new BitmapMat(d);
 
        
        public static unsafe byte[,] ToGrayArray(Bitmap bitmap)
        {

            
            
            int     w    = bitmap.Width, h = bitmap.Height;
            byte[,] gray = new byte[h, w]; // [y, x] → intern: y * w + x

            var rect = new Rectangle(0, 0, w, h);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);       // Versucht Konversion nach 24bppRGB

            try
            {
                byte* src0   = (byte*)data.Scan0;
                int   stride = data.Stride;
                
                if (stride < 0)
                {
                    src0   += stride * (h - 1);
                    stride =  -stride;
                }

                fixed (byte* grayPtr = gray)
                {
                    byte* dst = grayPtr;

                    for (int y = 0; y < h; y++)
                    {
                        byte* src = src0 + y * stride;

                        for (int x = 0; x < w; x++, dst++)
                        {
                            byte b = *src++;
                            byte g = *src++;
                            byte r = *src++;

                            *dst = (byte)((77 * r + 150 * g + 29 * b) >> 8);
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return gray;
        }

        /// <summary>
        /// Inverts all pixels.
        /// </summary>
        public void Invert()
        {
            for(int i=0; i < _grayByteArray.GetLength(0); i++)
                for (int j = 0; j < _grayByteArray.GetLength(1); j++)
                    _grayByteArray[i, j] =(byte) (1 ^ _grayByteArray[i, j]);

        }

    }
    
    public static class MarshallingMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId
            (
            IntPtr   hWnd,
            out uint lpdwProcessId
            );
        
    }
}
