RULER brings a photo of (damaged) vehicle to scale, by adjusting its resolution (pixel per cm) to an integer value. As a prequesite, a certain measure in the plane captured by the photo has to be known, for example the vehicle's wheelbase or a distance on a ruler (metro plegable) leant to the vehicle.

Especially photographs by the (German) police are often not to scale, because the main axis of the photo camera is not perpendicular to the plane of interest. Instead, the photos are often taken from eye height, distorting the perspective in vertical direction. RULER can correct this vertical distortion.

Preferably, RULER should be called by an photo viewer that specifies the selection area. At the moment, this viewer must be IrfanView, specifying the selection area in it's title bar. 
If the selection has protrait format (i.e. is higher than wide), the reference measurement is assumed to stretch in vertical direction, otherwise (land scape selection) in horizontal direction (for example the wheelbase).

If the reference object stretches in vertical direction (like a police ruler leaned to the car), a possible perspective distortion in vertical direction is corrected by RULER. 

RULER asks the user to supply the following input values.

1. Optional and only if the selection area has portrait format: Position of the middle measurement on the ruler in pixels. If no value is supplied, the distortion correction is skipped.

2. Real world distance of the larger extend of the selection area in centimeters.

3. Real world distance between the horizontal guiding lines drawn onto the (rectified) photo in centimeters.

If the selection has landscape format, it's lower side denotes the reference level (in most cases the ground level) and the guiding lines are drawn in reference to this level.

If no selection is provided, RULER first asks for the stretch of the reference distance (vertical or horizontal) and then for the upper and lower (respectively left and right) value in pixels.

...

RULER can be called from within IrfanView by inserting the following line into IrfanView's INI file:
ExternalViewer9=Path_to_Ruler.exe ""%1" /iv"

The parameter "/iv" signals RULER that it was called by IrfanView, providing the image filename an a selection area (in it's title text). In IrfanView, up to ten different external viewers can be called by pressing Crtl + [0 .. 9].

As RULER has to search for the IrfanView window, it may run into trouble, if more than one IrfanView window is open. You can prevent this by enabling the built-in single-instance setting or by using a command-line flag /one.