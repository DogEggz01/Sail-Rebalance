# Sail Reblance
*Disclaimer: This mod is made with ChatGPT 5.6 Sol.*

Junk and Gaff sail have 75% and 85% power modifier built in, means they are less powerfull than other sail.

Now Junk and Gaff have power curve that peak at certain apparent wind angle.

At peak angle, Junk and Gaff will perform same as other sail given same sail area.

Junk also received a strong wind bonus, if ture wind speed is more than 21 knots, the base modifier increase to 85%.

Junk and Gaff power curve can be disable seperately.

After disable power curve. You can use slider in Configuration menu to change the power modifier directly. Range 50% to 100%.
## Junk Curve ##
- **Modifier changes**: 0.75 at 90 degrees, 1.00 at 150 degrees, and 0.85 at 180 degrees.
- **Modifer under strong wind**: 0.85 at 90 degrees, 1.00 at 150 degrees, and 0.90 at 180 degrees.
- Below 90 degree remain unchanged compare to vanilla.
## Gaff Curve ##
- **Modifier changes**: 0.85 at 80 degrees, 1.00 at 120 degrees, and 0.85 at 160 degrees.
- Below 80 degree and above 160 degree remain unchanged compare to vanilla.
## Lateen change ##
- Bad tack will now receive 90% power modifier. (Could be toggle off)
- Add Lower brace winch for dipping maneuver of lateen yard.
- After tacking the yard to other side, penelty will be lifted.
- Dipping procedure:
  - Fully furl your sail and loose the sheet
  - Pull in Lower brace winch till yard tack to other side of mast. You cannot interact with Lower brace winch before halyard fully pulled in and sheet winch fully let out.
  - Release the lower brace till yard resume normal position. You cannot interact with Halyard and Sheet winch before yard return to normal position.
  - Set sail!
- After pulling Lower brace all the way in to tack the yard,  Lower brace winch need to be released at least 30% before pulling in to be able to tack the yard again.
  - This is to prevent accidently tacking the yard again
