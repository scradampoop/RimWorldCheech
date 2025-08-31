
# Cheech Xenotype Mod
scradam and leetle ShawShaw's first attempt at making a xenotype fan club mod for their cats.

Inspired and based upon the Orassans mod by Diana (Kitty) Winters at https://steamcommunity.com/sharedfiles/filedetails/?id=1541519487  .

**Q:** Why are they called 'Cheech' or 'Cheechers'?
<br/> **A:** scradam and ShawShaw have brain damage. That's literally what they call cats IRL, and now you probably will too. (Sorry.)
<br/> **Q:** Why is the root namespace "Cheechin"?
<br/> **A:** That's what cheechers do. They cheech! They be cheechin' (slang for cheeching). When you work on this mod, you be cheechin' too.

## Demo:

Here you can see we're attempting some new things by using pawn skin and hair color along with white and black to increase the readily available and configurable pawn texture palette.
![Demo Image](About/Demo.jpg)
![Cover Image](About/Preview.png)

## TODO:
- Implement change-fur like Anthrosonae.
- Separate fur patterns into new layers above skin/fur layer.
- Make fur colors independent and manage via Anthrosonae change-fur like modal.
- Finish the round bib base pattern.
- Finish the Strips-spots accent pattern.
- Finish the tux base pattern.
- Put Meetch patterns into game.
- Put Tweater-pea patterns into game.
- Put Leetis patterns into game.
- Go through z_samples folder and look for anything else good to put in game.
- Make sure XML is set up for all base and accent patterns.
- Add more tails. TBD on how to implement stripes/spots and color patterns on tails. 
- Make habit of putting latest patch notes and date in loading screen description.
- Replace tufts icon with smaller one with transparent background so it shows the gene background behind it. normalize color so it looks consistent with other .75 icons.
- Different icons for white vs skin pattern vs hair pattern.
- Change gene display order to be inline with similar genes or consider putting them in a standalone category.
- Reconsider rendering coat under tattoo layer.
- Put a link to the github repo in the About so people always know where to find the source code.
- Changing fur color should require tincture flower stuff like normal color changes.
- Custom names for faction/race members.
- Credit source mods in source code summaries once the code starts to stabilize.
- Figure out the bug with fur patterns not always rendering when naked in various UIs.

## Other Ideas:
- Abstract out the fur pattern system into shared lib for other mods to use.
- Figure out how the gradient hair mod works, particularly via character editor, and try to make that work for fur patterns.

## Known Issues:
- Fur patterns do not show up correctly in character editor, but they do still seem to work in game.

## Garbage:

misc. commands scradam is keeping somewhere for reference:

mklink /J "C:\\Steam\\steamapps\\common\\RimWorld\\Mods\\CheechXenoType" "C:\\rw\\RimWorldCheech"
mklink /J "C:\\rw\\RimWorld" "C:\\Steam\\steamapps\\common\\RimWorld"
mklink /J "C:\\rw\\workshop" "C:\\Steam\\steamapps\\workshop\\content\\294100"
