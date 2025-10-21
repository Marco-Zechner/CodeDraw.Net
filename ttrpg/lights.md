shadow for topdown





LitPixels\[];

WallPixelsToCheck\[];



foreach light in lights {

&nbsp;	HoL = getHeightOfLight()

&nbsp;	HoF = getHeightOfFloorBelowLight()

&nbsp;	(FloorPixels\[], OutlinePixels\[]) = FloodfillFloorAtHeight(PosLight, HoF)

&nbsp;	LitPixels += FloorPixels

&nbsp;	foreach WallPixel in OutlinePixels {

&nbsp;		HoWP = getHeightOfPixel(WallPixel)

&nbsp;		if (HoWP <  HoL) {

&nbsp;			(LitPixelsOnWall\[], WallOutlinePixels\[]) += FloodfillFoorAtHeight(WallPixel, HoWP)

&nbsp;			LitPixels += LitPixelsOnWall

&nbsp;			WallOutlinePixels -= LitPixels // only keep pixels on the far side of the wall, so the edge that will throw a shadow

&nbsp;			continue;

&nbsp;		}

&nbsp;		

&nbsp;	

&nbsp;	}



}





// 1 light only

foreach Pixel in Image {

&nbsp;	float SafePixelHeigth = Pixel.y

&nbsp;	foreach (PixelWalk in PixelsToLight(Pixel, Light) {

&nbsp;		if (PixelWalk.y < SafePixelHeight) continue;

&nbsp;		

&nbsp;		float lightHeight = LineHeightAt(PixelWalk, LineFromTo(Pixel, Light)

&nbsp;		if (lightHeight > PixelWalk.y || lightHeight <= PixelWalk.Bottom.y) {

&nbsp;			SafePixelHeight = lightHeight;

&nbsp;			continue;

&nbsp;		}



&nbsp;		Pixel.Lit = false;

&nbsp;		goto Next;

&nbsp;		

&nbsp;	}

&nbsp;	Pixel.Lit = true

&nbsp;	

&nbsp;	NEXT:

}

