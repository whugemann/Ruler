This C# program straightens out (rectifies) photos of (mostly) damaged cars that were taken
from eýe height, using a ruler (metro plegable) leaned to the car, looking down on the damage.
It makes use of Magick.NET to perform this task.

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
