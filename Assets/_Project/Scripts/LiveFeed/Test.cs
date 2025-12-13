using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.IO;
#if ENABLE_WINMD_SUPPORT

using Windows.Graphics.Imaging;
using FFmpegInterop;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics;
using System.Runtime.InteropServices;

#endif
using System;

#if ENABLE_WINMD_SUPPORT

// Helper class: https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture-video#direct3d-and-sharpdx-helper-apis

[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComVisible(true)]
interface IDirect3DDxgiInterfaceAccess
{
    IntPtr GetInterface([In] ref Guid iid);
};

public static class Direct3D11Helpers
{
    internal static Guid IInspectable = new Guid("AF86E2E0-B12D-4c6a-9C5A-D7AA65101E90");
    internal static Guid ID3D11Resource = new Guid("dc8e63f3-d12b-4952-b47b-5e45026a862d");
    internal static Guid IDXGIAdapter3 = new Guid("645967A4-1392-4310-A798-8053CE3E93FD");
    internal static Guid ID3D11Device = new Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140");
    internal static Guid ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    [DllImport(
        "d3d11.dll",
        EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice",
        SetLastError = true,
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        CallingConvention = CallingConvention.StdCall
        )]
    internal static extern UInt32 CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport(
        "d3d11.dll",
        EntryPoint = "CreateDirect3D11SurfaceFromDXGISurface",
        SetLastError = true,
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        CallingConvention = CallingConvention.StdCall
        )]
    internal static extern UInt32 CreateDirect3D11SurfaceFromDXGISurface(IntPtr dxgiSurface, out IntPtr graphicsSurface);

    public static IDirect3DDevice CreateD3DDevice()
    {
        return CreateD3DDevice(false);
    }

    public static IDirect3DDevice CreateD3DDevice(bool useWARP)
    {
        var d3dDevice = new SharpDX.Direct3D11.Device(
            useWARP ? SharpDX.Direct3D.DriverType.Software : SharpDX.Direct3D.DriverType.Hardware,
            SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);
        IDirect3DDevice device = null;

        // Acquire the DXGI interface for the Direct3D device.
        using (var dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device3>())
        {
            // Wrap the native device using a WinRT interop object.
            uint hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pUnknown);

            if (hr == 0)
            {
                device = Marshal.GetObjectForIUnknown(pUnknown) as IDirect3DDevice;
                Marshal.Release(pUnknown);
            }
        }

        return device;
    }


    internal static IDirect3DSurface CreateDirect3DSurfaceFromSharpDXTexture(SharpDX.Direct3D11.Texture2D texture)
    {
        IDirect3DSurface surface = null;

        // Acquire the DXGI interface for the Direct3D surface.
        using (var dxgiSurface = texture.QueryInterface<SharpDX.DXGI.Surface>())
        {
            // Wrap the native device using a WinRT interop object.
            uint hr = CreateDirect3D11SurfaceFromDXGISurface(dxgiSurface.NativePointer, out IntPtr pUnknown);

            if (hr == 0)
            {
                surface = Marshal.GetObjectForIUnknown(pUnknown) as IDirect3DSurface;
                Marshal.Release(pUnknown);
            }
        }

        return surface;
    }



    internal static SharpDX.Direct3D11.Device CreateSharpDXDevice(IDirect3DDevice device)
    {
        var access = (IDirect3DDxgiInterfaceAccess)device;
        var d3dPointer = access.GetInterface(ID3D11Device);
        var d3dDevice = new SharpDX.Direct3D11.Device(d3dPointer);
        return d3dDevice;
    }

    internal static SharpDX.Direct3D11.Texture2D CreateSharpDXTexture2D(IDirect3DSurface surface)
    {
        var access = (IDirect3DDxgiInterfaceAccess)surface;
        var d3dPointer = access.GetInterface(ID3D11Texture2D);
        var d3dSurface = new SharpDX.Direct3D11.Texture2D(d3dPointer);
        return d3dSurface;
    }


    public static SharpDX.Direct3D11.Texture2D InitializeComposeTexture(
        SharpDX.Direct3D11.Device sharpDxD3dDevice,
        SizeInt32 size)
    {
        var description = new SharpDX.Direct3D11.Texture2DDescription
        {
            Width = size.Width,
            Height = size.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
            SampleDescription = new SharpDX.DXGI.SampleDescription()
            {
                Count = 1,
                Quality = 0
            },
            Usage = SharpDX.Direct3D11.ResourceUsage.Default,
            BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource | SharpDX.Direct3D11.BindFlags.RenderTarget,
            CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None,
            OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None
        };
        var composeTexture = new SharpDX.Direct3D11.Texture2D(sharpDxD3dDevice, description);


        using (var renderTargetView = new SharpDX.Direct3D11.RenderTargetView(sharpDxD3dDevice, composeTexture))
        {
            sharpDxD3dDevice.ImmediateContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));
        }

        return composeTexture;
    }
}
#endif

public class Test : MonoBehaviour
{
    RawImage RawImageTest;
    Texture2D newTexture;
    public String RTSPURL;

#if ENABLE_WINMD_SUPPORT
    FFmpegInteropMSS decoder;
    MediaPlayer FrameServer;
    private IDirect3DSurface surface;
    private SharpDX.Direct3D11.Device dstDevice;

    private async void InitializeMediaPlayer()
    {
        SharpDX.Direct3D11.Device srcDevice = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware);
        Texture2D deviceTexture = new UnityEngine.Texture2D(768, 1024, UnityEngine.TextureFormat.RGBA32, false);
        IntPtr txPtr = deviceTexture.GetNativeTexturePtr();
        SharpDX.Direct3D11.Texture2D dstTexture = new SharpDX.Direct3D11.Texture2D(txPtr);
        dstDevice = dstTexture.Device;

        //Create sharedResource

        SharpDX.Direct3D11.Texture2DDescription sharedTextureDesc = dstTexture.Description;
        sharedTextureDesc.OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.Shared;
        SharpDX.Direct3D11.Texture2D m_DstTexture = new SharpDX.Direct3D11.Texture2D(dstDevice, sharedTextureDesc);

        SharpDX.Direct3D11.ShaderResourceViewDescription rvdesc = new SharpDX.Direct3D11.ShaderResourceViewDescription
        {
            Format = sharedTextureDesc.Format,
            Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D
        };

        rvdesc.Texture2D.MostDetailedMip = 0;
        rvdesc.Texture2D.MipLevels = 1;
        SharpDX.Direct3D11.ShaderResourceView rvptr = new SharpDX.Direct3D11.ShaderResourceView(
               dstDevice,
               m_DstTexture, rvdesc);

        newTexture = Texture2D.CreateExternalTexture(sharedTextureDesc.Width, sharedTextureDesc.Height, UnityEngine.TextureFormat.BGRA32, false, false, rvptr.NativePointer);

        var sharedResourceDst = m_DstTexture.QueryInterface<SharpDX.DXGI.Resource>();
        var sharedTexDst = srcDevice.OpenSharedResource<SharpDX.Direct3D11.Texture2D>(sharedResourceDst.SharedHandle);
        surface = Direct3D11Helpers.CreateDirect3DSurfaceFromSharpDXTexture(sharedTexDst);
        sharedResourceDst.Dispose();
        sharedTexDst.Dispose();
        dstTexture.Dispose();
        m_DstTexture.Dispose();

        //FFmpegInteropLogging.SetDefaultLogProvider();
        FFmpegInteropConfig configuration = new FFmpegInteropConfig()
        {
            MaxVideoThreads = 8,
            SkipErrors = uint.MaxValue,
            DefaultBufferTime = TimeSpan.Zero,
            FastSeek = true,
            VideoDecoderMode = VideoDecoderMode.ForceFFmpegSoftwareDecoder,
        };

        //configuration.FFmpegOptions.Add("tune", "zerolatency");
        //configuration.FFmpegOptions.Add("flags", "low_delay");
        //configuration.FFmpegOptions.Add("fflags", "discardcorrupt+shortest+sortdts+ignidx+nobuffer");
        //decoder = await FFmpegInteropMSS.CreateFromUriAsync(RTSPURL, configuration);

        decoder = await FFmpegInteropMSS.CreateFromUriAsync("rtsp://rtsp.stream/pattern");

        var mediaStreamSource = decoder.GetMediaStreamSource();
        mediaStreamSource.BufferTime = TimeSpan.FromSeconds(0);
        Debug.Log($"{decoder.CurrentVideoStream.CodecName} {decoder.CurrentVideoStream.DecoderEngine} {decoder.CurrentVideoStream.HardwareDecoderStatus}  {decoder.CurrentVideoStream.PixelWidth} x {decoder.CurrentVideoStream.PixelHeight}");
        FrameServer = new Windows.Media.Playback.MediaPlayer() { IsVideoFrameServerEnabled = true };
        FrameServer.Source = MediaSource.CreateFromMediaStreamSource(mediaStreamSource);
        FrameServer.RealTimePlayback = true;
        FrameServer.VideoFrameAvailable += MediaPlayer_VideoFrameAvailable;
        FrameServer.AutoPlay = true;
        FrameServer.Play();

        Debug.Log("Initialzed");

        RawImageTest.enabled = true;
    }

    private void MediaPlayer_VideoFrameAvailable(Windows.Media.Playback.MediaPlayer sender, object args)
    {
        Debug.Log("Got new frame");
        sender.CopyFrameToVideoSurface(surface);
        Debug.Log("Frame copied");
    }
#endif

    // Start is called before the first frame update
    void Start()
    {
#if UNITY_WSA && ENABLE_WINMD_SUPPORT
        RawImageTest = GetComponent<RawImage>();
        RawImageTest.enabled = false;

        InitializeMediaPlayer();

        RawImageTest.texture = newTexture;

        Debug.Log("Hello there");
#endif
    }

    // Update is called once per frame
    void Update()
    {
#if UNITY_WSA && ENABLE_WINMD_SUPPORT
#endif
    }
}
