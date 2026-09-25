using System;
using UnityEngine;
namespace FAA.Customization
{
    public static class FaaWebcamFrameMath
    {
        // Both arrays use Unity bottom-left order. Output is upright and unmirrored for recognition.
        public static void Upright(Color32[] source,int width,int height,Color32[] output,
            int outputWidth,int outputHeight,int clockwiseDegrees,bool verticalMirror)
        {
            if(source==null||output==null||width<=0||height<=0||outputWidth<=0||outputHeight<=0||
                source.Length!=width*height||output.Length!=outputWidth*outputHeight) throw new ArgumentException("Invalid webcam dimensions.");
            int angle=((clockwiseDegrees%360)+360)%360;
            if(angle%90!=0) throw new ArgumentException("Unsupported camera orientation.");
            for(int y=0;y<outputHeight;y++) for(int x=0;x<outputWidth;x++)
            {
                float u=(x+.5f)/outputWidth,v=(y+.5f)/outputHeight,su=u,sv=v;
                if(angle==90){su=1-v;sv=u;} else if(angle==180){su=1-u;sv=1-v;} else if(angle==270){su=v;sv=1-u;}
                if(verticalMirror)sv=1-sv;
                int sx=Mathf.Clamp((int)(su*width),0,width-1),sy=Mathf.Clamp((int)(sv*height),0,height-1);
                output[y*outputWidth+x]=source[sy*width+sx];
            }
        }
    }
}
