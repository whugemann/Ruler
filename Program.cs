/*************************************************************
The program straightens out (rectifies) photos of (mostly) damaged cars that were taken
from eýe height, using a ruler (metro plegable) leaned to the car, looking down on the damage.

The file name of the photo must be passed as a parameter (otherwise the program will abort).

The program requires the following input values:
   1: Position of the top measurement on the stick in pixels  (e.g., at 80 cm)
   2: Position of the middle measurement on the bar in pixels (e.g., at 45 cm)
   3: Position of the lowest measurement on the bar in pixels (e.g., at 10 cm)
   4: Number of desired horizontal guide lines      (e.g.,  8 => 10 cm intervals)

The program can be called from IrfanView by inserting the following line into IrfanViews INI file,
i.e. setting an external viewer, which can be initiated via ctrl + number:
  ExternalViewer9=Path_to_the_exe_file_of_this_program “”%1“ /iv”

The parameter “/iv” signals that the current app was called by IrfanView.
In this case, the pixel values for the top and bottom dimensions are determined based on the selected area,
if available. The selection area is extracted from IrfanView's window title.
    
   © by Wolfgang Hugemann
*************************************************************/
using static System.Console;
using System.Runtime.InteropServices;
using System.Text;
using ImageMagick;
using ImageMagick.Drawing;
using HWND = System.IntPtr;
const string rulerVersion = "2025-07-07";

[DllImport("USER32.DLL")]
static extern int FindWindow(StringBuilder lpString, StringBuilder lpString1);
[DllImport("USER32.DLL")]
static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);
[DllImport("USER32.DLL")]
static extern int GetWindowTextLength(HWND hWnd);

[DllImport("kernel32", CharSet = CharSet.Unicode)]
static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

// Read Language from Ini-File
var RetVal = new StringBuilder(255);
string iniFileName = AppDomain.CurrentDomain.BaseDirectory + Path.ChangeExtension(AppDomain.CurrentDomain.FriendlyName, "ini");
GetPrivateProfileString("Settings", "Language", "Deutsch", RetVal, 255, iniFileName);
bool isGerman = (RetVal.ToString() == "Deutsch");
try
{
   // Endungen für die geänderten Dateien
   const string ext = "_pr";
   const string extl = "_prl";
   bool IsCalledByIV = false;
   uint upper = 0;
   uint middle = 0;
   uint lower = 0;
   uint linecount = 0;

   WriteLine($".NET Version { Environment.Version.ToString()}");
   WriteLine(MagickNET.Version);
   WriteLine($"Script Version {rulerVersion}");
   WriteLine("---------------------------------");
   WriteLine();
   // Some basic error checks
   if (args.Length == 0)
   {
      if (isGerman) { WriteLine("Kein Dateiname als Argument übergeben."); }
      else { WriteLine("Please specify a filename as the argument"); }
      ReadLine();
      return 1;
   }

   string InputFile = args[0];
   if (!File.Exists(InputFile))
   {
      if (isGerman) { WriteLine($"{InputFile} ist keine gültige Datei."); }
      else { WriteLine($"{InputFile} is not a valid file."); }
      ReadLine();
      return 1;
   }
   string ipExt = Path.GetExtension(InputFile);
   if (!MagickNET.SupportedFormats.Any(format => format.SupportsReading &&
            format.Format.ToString().Equals(ipExt.Substring(1), StringComparison.OrdinalIgnoreCase)))
   {
      if (isGerman) { WriteLine($"Die übergebene Datei *.{ipExt} ist kein Bild."); }
      else { WriteLine($"The provided file *.{ipExt} is not an image."); }
      ReadLine();
      return 1;
   }

   // The command line parameter /iv should have been set in IrfanView's ini file and signals that
   // the app was called by IrfanView as an external viewer, i.e. we can try to draw the selection from 
   // IrfanView's window title
   if (args.Length > 1)
   {
      if (args[1] == "/iv")
      {
         IsCalledByIV = true;
      }
   }
   // Search for the IrfanView window and check whether an area is selected:
   // Extract the pixel coordinates of the upper and lower measure readings on the ruler
   // from its specification -- "(Selection 318, 128; 200x300 ..."

   if (IsCalledByIV)
   {
      StringBuilder searchText = new StringBuilder("IrfanView");
      HWND hWnd = FindWindow(searchText, null);
      int length = GetWindowTextLength(hWnd);
      StringBuilder builder = new StringBuilder(length);
      GetWindowText(hWnd, builder, length + 1);
      string strIV = builder.ToString();
      
      if (strIV.Contains("Selection"))
      {
         string[] result = strIV.Split("Selection:", StringSplitOptions.RemoveEmptyEntries);
         char[] charSeps = new char[] { ';', ',', ' ', 'x' };
         string[] bps = result[1].Split(charSeps, StringSplitOptions.RemoveEmptyEntries);
         upper = Convert.ToUInt16(bps[1]);
         lower = upper + Convert.ToUInt16(bps[3]);
      }
      else { upper = 0; lower = 0; }
   }
   // In any case, the middle measurement and the number of guiding lines
   // has to be provided by the user. The upper and lower measurements may
   // have been extracted from IrfanView's window title.

   if (upper == 0)
   {
      if (isGerman) { upper = GetNumber("Oberes Maß (px)"); }
      else { upper = GetNumber("Upper Point (px)"); }
   }
   if (isGerman) { middle = GetNumber("Mittleres Maß (px)"); }
   else { middle = GetNumber("Middle Point (px)"); }
   if (lower == 0)
   {
     if (isGerman) { lower = GetNumber("Unteres Maß (px)"); }
     else { lower = GetNumber("Lower Point (px)"); }
   }
   if (isGerman) { linecount = GetNumber("Anzahl Linien"); }
   else { linecount = GetNumber("Line count"); }
   // Hier kommt die eigentlich Arbeit
   using var image = new MagickImage(InputFile);
   uint width = image.Width;
   double hmiddle = width / 2;
   double vmiddle = (upper + lower) / 2;

   string OutputFile = Path.ChangeExtension(InputFile, null) + ext + Path.GetExtension(InputFile);
   string OutputFileLines = Path.ChangeExtension(InputFile, null) + extl + Path.GetExtension(InputFile);

   // The distortion is assumed to be exactly (and exclusively) in vertical direction,
   // i.e. only one parameter suffices to provide the needed information and that is
   // the vertical position of the midpoint relative to the midth between the upper and lower coordinate.
   // Therefore three points are mapped exactly to themselves, whereas the vertical coordinate of the middle point is adjusted to the mean value
   // middle -> vmiddle

   GetPrivateProfileString("Settings", "Color", "#CCCCCCFF", RetVal, 255, iniFileName);
   string myColor = RetVal.ToString();
   image.BackgroundColor = new MagickColor(myColor);
   image.VirtualPixelMethod = VirtualPixelMethod.Background;
   image.Distort(DistortMethod.Perspective,
                   hmiddle, upper, hmiddle, upper,
                   0.0, lower, 0.0, lower,
                   width, lower, width, lower,
                   hmiddle, middle, hmiddle, vmiddle);

   image.Write(OutputFile);

   // Draw horizontal guiding lines over the rectified image
   // in equidistance, typically correponding to 10 cm
   // To optimise the contrast between these lines an the image,
   // the lines are drawn on a blanc copy of the original image and
   // then superimposed on the rectified image.

   DrawableFillColor fillColor = new DrawableFillColor(new MagickColor("White"));
   DrawableStrokeColor strokeColor = new DrawableStrokeColor(new MagickColor("White"));
   DrawableStrokeWidth strokeWidth = new DrawableStrokeWidth(image.Height / 700 + 1);

   double linestep = (double)(lower - upper) / (linecount - 1);
   using var image2 = new MagickImage(MagickColors.Black, image.Width, image.Height);
   for (uint i = 0; i < linecount; i++)
   {
      DrawableLine line = new DrawableLine(0, upper + i * linestep, image2.Width, upper + i * linestep);
      image2.Draw(strokeColor, strokeWidth, fillColor, line);
   }
   image.Composite(image2, CompositeOperator.Difference);
   image.Write(OutputFileLines);
   return 0;
}
catch (Exception ex)
{
   if (isGerman)
   {
      WriteLine($"Fehler: {ex.Message}");
      WriteLine("Beenden mit Enter-Taste.");
   }
   else
   {
      WriteLine($"Error: {ex.Message}");
      WriteLine("Press Enter to finish program.");
   }
   ReadLine();
   return 1;
}
// Sichere Eingabe einer positiven Ganzzahl über die Konsole
static uint GetNumber(string MsgText)
{
   uint number = 0;
   string? ui;
   do
   {
      Write($"{MsgText}: ");
      ui = ReadLine();
   } while (!uint.TryParse(ui, out number));
   return number;
}
