using ImageMagick;
using ImageMagick.Colors;
using ImageMagick.Drawing;
using Microsoft.VisualBasic;
using System;
using System.ComponentModel.Design;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;
using HWND = System.IntPtr;

namespace Ruler
{
   internal class Program
   {
      [DllImport("USER32.DLL")]
      private static extern int FindWindow(StringBuilder lpString, StringBuilder lpString1);
      [DllImport("USER32.DLL")]
      private static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);
      [DllImport("USER32.DLL")]
      private static extern int GetWindowTextLength(HWND hWnd);

      [DllImport("kernel32", CharSet = CharSet.Unicode)]
      private static extern uint GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

      public static bool isGerman = true;
      static int Main(string[] args)
      {
         // Read Language from Ini-File
         var RetVal = new StringBuilder(255);
         string iniFileName = AppDomain.CurrentDomain.BaseDirectory + Path.ChangeExtension(AppDomain.CurrentDomain.FriendlyName, "ini");
         GetPrivateProfileString("Settings", "Language", "xx-xx", RetVal, 255, iniFileName);

         if (RetVal.ToString() != "xx-xx")
         {
            CultureInfo customCulture = new CultureInfo(RetVal.ToString());
            CultureInfo.DefaultThreadCurrentUICulture = customCulture;
            Thread.CurrentThread.CurrentUICulture = customCulture;
         }

         //try
         //{
         // Additions to the file name of the rectified image and the one with guiding lines
         const string ext = "_pr";
         const string extl = "_prl";
         bool IsCalledByIV = false;
         bool doTransform = true;
         uint middle = 0;
         uint distanceCentimeter = 0;
         uint deltaCentimeter = 0;
         uint linecount = 0;
         CompositeFormat? format;
         string message;

         string? buildVersion = Assembly.GetExecutingAssembly()
         .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
         .InformationalVersion;

         Console.WriteLine($"{buildVersion}");
         Console.WriteLine();

         // Some basic error checks
         if (args.Length == 0)
         {
            Console.WriteLine(Resources.noArguments);
            Console.ReadLine();
            return 1;
         }

         if ((args[0] == "/?") || (args[0] == "-?") || (args[0] == "/h") || (args[0] == "-h") || (args[0] == "--help"))
         {
            Console.WriteLine(Resources.helpText);
            Console.ReadLine();
            return 0;
         }
         string inputFile = args[0];
         if (!File.Exists(inputFile))
         {
            format = CompositeFormat.Parse(Resources.noValidFile);
            message = string.Format(null, format, inputFile);
            Console.WriteLine(message);
            Console.ReadLine();
            return 1;
         }
         string ipExt = Path.GetExtension(inputFile);
         if (!MagickNET.SupportedFormats.Any(format => format.SupportsReading &&
                  format.Format.ToString().Equals(ipExt.Substring(1), StringComparison.OrdinalIgnoreCase)))
         {
            format = CompositeFormat.Parse(Resources.noImage);
            message = string.Format(null, format, ipExt);
            Console.WriteLine(message);
            Console.ReadLine();
            return 1;
         }


         // The command line parameter /iv should have been set in IrfanView's ini file and signals that
         // the app was called by IrfanView as an external viewer, i.e. we can try to draw the selection from 
         // IrfanView's window title
         if ((args.Length > 1) && (args[1] == "/iv"))
         {
            IsCalledByIV = true;
         }

         bool selectionAvailable = false;
         bool portraitSelection = true;
         uint left = 0, top = 0, right = 0, bottom = 0;
         double aspectRatio = 0.0;

         if (IsCalledByIV)
         {
            selectionAvailable = GetIVSelection(out left, out top, out right, out bottom);
            if (selectionAvailable)
            {
               aspectRatio = (double)(bottom - top) / (right - left);
               portraitSelection = aspectRatio > 1.0;
            }
         }

         // In any case, the middle measurement and the number of guiding lines
         // has to be provided by the user. The upper and lower measurements may
         // have been extracted from IrfanView's window title.
         // If the user does not provide a middle value, the rectification is not performed, but only the guiding lines are drawn over the original image.

         var image = new MagickImage(inputFile);

         uint deltaPx;
         if (!selectionAvailable)
         {  
            Console.WriteLine(Resources.selectionNotAvailable);
            portraitSelection = getBool(Resources.portraitSelection);
            if (portraitSelection)
            {
               top = GetNumber(Resources.upperPoint, 0, 0, image.Height);
               bottom = GetNumber(Resources.lowerPoint, top, (uint)(image.Height / 2), image.Height);
            } else {
               left = GetNumber(Resources.leftPoint, 0, 0, image.Width);
               right = GetNumber(Resources.rightPoint, left, (uint)(image.Width / 2), image.Width);
            }
         }

         uint middlePx = (uint)((top + bottom) / 2);
         if (portraitSelection)
         {
           deltaPx = bottom - top;
           uint difPx = (uint)(deltaPx * 0.2);
           middle = GetNumber(Resources.middlePoint, middlePx, middlePx - difPx, middlePx + difPx);
           doTransform = !(middle == middlePx);
         } else {
           deltaPx = right - left;
           doTransform = false;
         }

         // Get real world distance between the reference points in [cm]
         distanceCentimeter = GetNumber(Resources.totalDistance, 100, 10, 300);
         deltaCentimeter = GetNumber(Resources.lineDistance, 10, 0, distanceCentimeter);

         // Suggest scaling
         double ppcm = (double)deltaPx / distanceCentimeter;
         uint ippcm = 5 * (uint)Math.Round(ppcm / 5);

         format = CompositeFormat.Parse(Resources.getPpcm);
         message = string.Format(null, format, ppcm, ippcm);
         ippcm = GetNumber(message, ippcm, 5, 2 * ippcm);
         double scaling = (double)ippcm / ppcm;

         image.Resize((uint)(scaling * image.Width), (uint)(scaling * image.Height));

         double dtop = top * scaling;
         double dmiddle = middle * scaling;
         double dbottom = bottom * scaling;

         // Set scale to 1:10
         image.Density = new Density(ippcm * 10, ippcm * 10, DensityUnit.PixelsPerCentimeter);

         string OutputFile = Path.ChangeExtension(inputFile, null) + ext + Path.GetExtension(inputFile);
         string OutputFileLines = Path.ChangeExtension(inputFile, null) + extl + Path.GetExtension(inputFile);

         if (doTransform)
         {

            // Let's do the rectification of the image based on the three measurements and draw the guiding lines

            uint width = image.Width;
            double hmiddle = width / 2;
            double vmiddle = (dtop + dbottom) / 2;

            // The distortion is assumed to be exactly (and exclusively) in vertical direction,
            // i.e. only one parameter suffices to provide the needed information and that is
            // the vertical position of the midpoint relative to the midth between the upper and lower coordinate.
            // Therefore three points are mapped exactly to themselves, whereas the vertical coordinate of the middle point is adjusted to the mean value
            // middle -> vmiddle

            GetPrivateProfileString("Settings", "BackgroundColor", "White", RetVal, 255, iniFileName);
            string myColor = RetVal.ToString();
            image.BackgroundColor = new MagickColor(myColor);
            image.VirtualPixelMethod = VirtualPixelMethod.Background;
            image.Distort(DistortMethod.Perspective,
                      hmiddle, dtop, hmiddle, dtop,
                      0.0, dbottom, 0.0, dbottom,
                      width, dbottom, width, dbottom,
                      hmiddle, dmiddle, hmiddle, vmiddle);

         };
         image.Write(OutputFile);

         // Draw horizontal guiding lines over the rectified image
         // in equidistance, typically correponding to 10 cm
         // To optimise the contrast between these lines an the image,
         // the lines are drawn on a blanc copy of the original image and
         // then superimposed on the rectified image.

         // Set line Color from ini file, default is white
         GetPrivateProfileString("Settings", "Color", "White", RetVal, 255, iniFileName);
         var color = new MagickColor("White");
         try
         {
            color = new MagickColor(RetVal.ToString());
         }
         catch (ArgumentException)
         {
            color = new MagickColor("White");
         }
         DrawableStrokeColor strokeColor = new DrawableStrokeColor(new MagickColor(color));

         // Set line type from ini file, default is dashed
         GetPrivateProfileString("Settings", "DashArray", "20", RetVal, 255, iniFileName);

         string dashString = RetVal.ToString();
         DrawableStrokeDashArray strokeDashArray;
         try
         {
            IEnumerable<double> dashArray = dashString.Split(',').Select(double.Parse);
            strokeDashArray = new DrawableStrokeDashArray(dashArray);
         }
         catch (ArgumentException)
         {
            strokeDashArray = new DrawableStrokeDashArray(20, 30);
         }

         DrawableStrokeWidth strokeWidth = new DrawableStrokeWidth(Math.Sqrt(ippcm) / 3);

         using var image2 = new MagickImage(MagickColors.Black, image.Width, image.Height);

         double linestep = deltaCentimeter * ippcm;

         if (portraitSelection)
         {
            linecount = (uint)(distanceCentimeter / deltaCentimeter) + 1;
         } else {
            double dright = right * scaling;
            double dleft = left * scaling;
            distanceCentimeter = (uint)(dbottom / (dright - dleft) * distanceCentimeter);
            linecount = (uint)(distanceCentimeter / deltaCentimeter) + 1;
         }

         for (uint i = 0; i < linecount; i++)
         {
            DrawableLine line = new DrawableLine(0, dbottom - i * linestep, image2.Width, dbottom - i * linestep);
            image2.Draw(strokeColor, strokeWidth, strokeDashArray, line);
         }
         image.Composite(image2, CompositeOperator.Difference);
         image.Write(OutputFileLines);
         return 0;
      }
      // Securely read a uint number from the console, i.e. only accept a valid uint value
      private static uint GetNumber(string MsgText, uint defval = 0, uint min = 0, uint max = UInt32.MaxValue)
      {
         uint number = 0;
         string? ui;
         do
         {
            Console.Write($"{MsgText}: ");
            ui = Console.ReadLine();
            if (string.IsNullOrEmpty(ui))
            {
               return defval;
            } else
            {
               if (uint.TryParse(ui, out number))
               {
                  if (number < min || number > max)
                  {
                     var format = CompositeFormat.Parse(Resources.minMax);
                     string message = string.Format(null, format, min, max);
                     Console.WriteLine(message);
                     continue;
                  }
               }
               else
               {
                  Console.WriteLine(Resources.noValidNumber);
                  continue;
               }
            }
            return number;
         } while (true);

      }

      private static bool getBool(string MsgText)
      {
         string? ui;
         bool result = true;
         
         Console.Write($"{MsgText}: ");
         ui = Console.ReadLine();
         if (!string.IsNullOrEmpty(ui))
         {
              result = false;
         }
         return result;
       }
      // Search for the IrfanView window and check whether an area is selected:
      // Extract the pixel coordinates of the upper and lower measure readings on the ruler
      // from its specification -- "(Selection 318, 128; 200x300 ..."

      private static bool GetIVSelection(out uint left, out uint top, out uint right, out uint bottom)
      {
         left = 0; top = 0; right = 0; bottom = 0;
         HWND hWnd = (HWND)FindWindow(new StringBuilder("IrfanView"), null);
         int length = GetWindowTextLength(hWnd);
         if (length == 0) { return false; }

         StringBuilder sb = new StringBuilder(length + 1);
         GetWindowText(hWnd, sb, length + 1);
         string title = sb.ToString();
         if (!title.Contains("Selection")) { return false; }

         string[] result = title.Split('(', StringSplitOptions.RemoveEmptyEntries);
         char[] charSeps = new char[] { ';', ',', ' ', 'x' };
         string[] bps = result[2].Split(charSeps, StringSplitOptions.RemoveEmptyEntries);
         left = Convert.ToUInt16(bps[1]);
         top = Convert.ToUInt16(bps[2]);
         right = left + Convert.ToUInt16(bps[3]);
         bottom = top + Convert.ToUInt16(bps[4]);
         return true;
      }
   }
}