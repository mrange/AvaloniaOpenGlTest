namespace AvaloniaOpenGlTest
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new Win32PlatformOptions
                    {
                        RenderingMode = 
                        [
                            Win32RenderingMode.Wgl
                        ],
                        WglProfiles = 
                        [
                            new(GlProfileType.OpenGL, 4, 5)
                        ],
                        CompositionMode =
                        [
                            Win32CompositionMode.WinUIComposition,
                            Win32CompositionMode.RedirectionSurface
                        ]
                    })
                .WithInterFont()
                .LogToTrace();
    }
}
