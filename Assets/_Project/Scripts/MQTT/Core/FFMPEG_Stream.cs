using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FFMPEG_Stream : MonoBehaviour
{ 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    /*
    private UnityEngine.Texture2D tex = new UnityEngine.Texture2D(320, 240, UnityEngine.TextureFormat.RGBA32, false);

    private async void InitializeMediaPlayer()
    {
        FFmpegInteropLogging.SetDefaultLogProvider();
        FFmpegInteropConfig configuration = new FFmpegInteropConfig()
        {
            MaxVideoThreads = 8,
            SkipErrors = uint.MaxValue,
            DefaultBufferTime = TimeSpan.Zero,
            FastSeek = true,
            VideoDecoderMode = VideoDecoderMode.ForceFFmpegSoftwareDecoder,
        };
        configuration.FFmpegOptions.Add("tune", "zerolatency");
        configuration.FFmpegOptions.Add("flags", "low_delay");
        configuration.FFmpegOptions.Add("fflags", "discardcorrupt+shortest+sortdts+ignidx+nobuffer");
        decoder = await FFmpegInteropMSS.CreateFromUriAsync("udp://127.0.0.1:9005", configuration);
    
                var mediaStreamSource = decoder.GetMediaStreamSource();
        mediaStreamSource.BufferTime = TimeSpan.FromSeconds(0);
        Debug.WriteLine($"{decoder.CurrentVideoStream.CodecName} {decoder.CurrentVideoStream.DecoderEngine} {decoder.CurrentVideoStream.HardwareDecoderStatus}  {decoder.CurrentVideoStream.PixelWidth} x {decoder.CurrentVideoStream.PixelHeight}");
        var FrameServer = new Windows.Media.Playback.MediaPlayer() { IsVideoFrameServerEnabled = true };
        FrameServer.Source = MediaSource.CreateFromMediaStreamSource(mediaStreamSource);
        FrameServer.RealTimePlayback = true;
        FrameServer.VideoFrameAvailable += MediaPlayer_VideoFrameAvailable;
        FrameServer.Play();
    }

    //FrameAvailable:
    private void MediaPlayer_VideoFrameAvailable(Windows.Media.Playback.MediaPlayer sender, object args)
    {
        CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();
        using (CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, frameServerDest))
        {

            sender.CopyFrameToVideoSurface(canvasBitmap);
            byte[] bytes = canvasBitmap.GetPixelBytes();

            if (AppCallbacks.Instance.IsInitialized())
            {
                AppCallbacks.Instance.InvokeOnAppThread(() =>
                {

                    tex.LoadRawTextureData(bytes);
                    tex.Apply();
                    Display.GetComponent<UnityEngine.UI.RawImage>().texture = tex;
                }, false);
            }
            GC.Collect();
        }
    }*/
}
