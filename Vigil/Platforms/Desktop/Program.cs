using Uno.UI.Hosting;

namespace Vigil;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
#if DEBUG
            // Screenshot runs under Xvfb: VIGIL_X11_RENDERER=software forces Skia's CPU backend instead of Mesa's
            // llvmpipe OpenGL, which left parts of the window unpainted after resizes (docs/PRODUCTION-READINESS.md).
            .UseX11(x11 =>
            {
                if (Environment.GetEnvironmentVariable("VIGIL_X11_RENDERER") == "software")
                {
                    x11.ForceRenderingBackend(X11RenderingBackend.Software);
                }
            })
#else
            .UseX11()
#endif
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
