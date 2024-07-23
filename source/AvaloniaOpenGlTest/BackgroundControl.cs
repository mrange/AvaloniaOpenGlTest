
using Lib.ShaderMixer.OpenGLExt;

using static Lib.ShaderMixer.OpenGLExt.GlConstsExt;

namespace AvaloniaOpenGlTest;

class BackgroundControl : Control
{
    
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct Vertex
    {
        public Vector3 Position;
        public Vector2 TexCoord;
    }

    record RenderScalingMessage(double RenderScaling);

    class DisposeMessage { }

    record struct GlProgram(int Program);
    record struct GlTexture(int Texture);

    class OpenGlState : IDisposable
    {
        IGlContext          _glContext              ;
#if DEBUG
        OpenGlDebugMode     _glDebugMode            ;
#endif

        GlProgram           _glProgram              ;
        GlTexture           _glTexture              ;

        const string _fragmentShaderSource = FragmentShaders.Texture;

        void CheckError()
        {
            var gl = _glContext.GlInterface;
            int err;
            while ((err = gl.GetError()) != GL_NO_ERROR) {
                throw new Exception($"Oops: {err}");
            }
        }

        void IgnoreError()
        {
            var gl = _glContext.GlInterface;
            int err;
            while ((err = gl.GetError()) != GL_NO_ERROR) {
                // Just spin until all errors are gone
            }
        }

        public void Dispose()
        {
            var gl      = _glContext.GlInterface;

            gl.BindTexture(GL_TEXTURE_2D, 0);
            CheckError();

            gl.UseProgram(0);
            CheckError();

            gl.DeleteProgram(_glProgram.Program);
            CheckError();

#if DEBUG
            _glDebugMode.Dispose ();
#endif
        }

        public unsafe OpenGlState(IGlContext glContext)
        {
            _glContext = glContext;
            CheckError();

            var gl = glContext.GlInterface;

#if DEBUG
            var glVersion           = gl.GetString(GL_VERSION);
            var glRenderer          = gl.GetString(GL_RENDERER);
            var glVendor            = gl.GetString(GL_VENDOR);
            var glShaderLangVersion = gl.GetString(GL_SHADING_LANGUAGE_VERSION);

            var exts = new StringBuilder($"Initialized OpenGL with Version={glVersion}, Renderer={glRenderer}, Vendor={glVendor}, Shader lang version={glShaderLangVersion}, Extensions=");
            string? ext;
            int exti = 0;
            while ((ext = gl.GetString(GL_EXTENSIONS, exti)) is not null)
            {
                exts.Append(ext).Append(",");
                ++exti;
            }
            Debug.WriteLine(exts.ToString());

            IgnoreError();
#endif

#if DEBUG
            var glext = new GlInterfaceExt(gl);
            _glDebugMode = new OpenGlDebugMode (gl, glext);
            CheckError();
#endif

            _glProgram = new (glext.CreateShaderProgramv(GL_FRAGMENT_SHADER, _fragmentShaderSource));
            CheckError();

            _glTexture = new (gl.GenTexture());

            gl.BindTexture (GL_TEXTURE_2D, _glTexture.Texture);
            CheckError();

            var rnd = Random.Shared;
            const int NoOfPoints = 64*64;
            var bytes = new byte[NoOfPoints*4];
            rnd.NextBytes (bytes);

            fixed (void* data = bytes)
            {
                gl.TexImage2D (GL_TEXTURE_2D, 0, GL_RGBA8, 64, 64, 0, GL_RGBA, GL_UNSIGNED_BYTE, new(data));
            }
            CheckError();
        }

        public void DrawGl(PixelSize size, float renderScaling, float time)
        {
            var gl = _glContext.GlInterface;
            var glext = new GlInterfaceExt(gl);

            var ratio = ((float)size.Width)/((float)size.Height);

            gl.ClearColor(1, 0, 0, 1);
            gl.Clear(GL_COLOR_BUFFER_BIT);
            CheckError();

            /*
            gl.Viewport(
                    0
                ,   0
                ,   (int)(size.Width*renderScaling)
                ,   (int)(size.Height*renderScaling)
                );
            CheckError();
            */

            gl.ActiveTexture(GL_TEXTURE0);
            CheckError();

            gl.BindTexture (GL_TEXTURE_2D, _glTexture.Texture);
            CheckError();

            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
            CheckError();

            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
            CheckError();

            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);
            CheckError();

            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
            CheckError();

            gl.UseProgram(_glProgram.Program);
            CheckError();

            glext.Uniform2f (0, size.Width, size.Height);
            CheckError();

            glext.Uniform1i(1, 0);
            CheckError();

            glext.Rects(-1,-1,1,1);
        }

        public bool HasSameContext(IGlContext glContext)
        {
            return ReferenceEquals(_glContext, glContext);
        }

    }

    class OpenGlVisual : CompositionCustomVisualHandler, IDisposable
    {
        readonly Stopwatch _sw;
        OpenGlState? _glState = null;

        double _renderScaling = 1;

        public OpenGlVisual(Stopwatch sw)
        {
            _sw = sw;
        }

        public void Dispose()
        {
            var glState = _glState;
            _glState = null;
            glState?.Dispose();
        }

        public override void OnAnimationFrameUpdate()
        {
            Invalidate();

            base.OnAnimationFrameUpdate();
        }

        public override void OnMessage(object message)
        {
            switch(message)
            {
            case DisposeMessage disposeMessage:
                Dispose();
                break;
            case RenderScalingMessage renderScalingMessage:
                _renderScaling = renderScalingMessage.RenderScaling;
                break;
            }

            base.OnMessage(message);
        }

        public override void OnRender(ImmediateDrawingContext context)
        {
            RegisterForNextAnimationFrameUpdate();
            var bounds = GetRenderBounds();
            var pixelSize = PixelSize.FromSize(bounds.Size, 1);
            if (pixelSize.Width < 1 || pixelSize.Height < 1)
                return;

            if(context.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var skiaFeature))
            {
                using var skiaLease = skiaFeature.Lease();
                var grContext = skiaLease.GrContext;
                if (grContext == null)
                    return;

                using var platformApiLease = skiaLease.TryLeasePlatformGraphicsApi();
                if (platformApiLease?.Context is not IGlContext glContext)
                    return;

                if (!(_glState?.HasSameContext(glContext)??true))
                {
                    var glState = _glState;
                    _glState    = null;
                    glState.Dispose();
                }

                if (_glState is null)
                {
                    _glState = new(glContext);
                }

                _glState.DrawGl(pixelSize, (float)_renderScaling, _sw.ElapsedMilliseconds/1000F);
            }
        }
    }

    Stopwatch _sw = Stopwatch.StartNew();

    double _renderScaling = 1;
    public double RenderScaling 
    { 
        get
        {
            return _renderScaling;
        }
        set
        {
            _renderScaling = value;
            _visual?.SendHandlerMessage(new RenderScalingMessage(RenderScaling));
        }
    }

    CompositionCustomVisual? _visual;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var visual = ElementComposition.GetElementVisual(this);
        if(visual is null)
            return;

        _visual = visual.Compositor.CreateCustomVisual(new OpenGlVisual(_sw));
        ElementComposition.SetElementChildVisual(this, _visual);
        _visual.Size = new (Bounds.Width, Bounds.Height); 
        _visual.SendHandlerMessage(new RenderScalingMessage(RenderScaling));

        base.OnAttachedToVisualTree(e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _visual?.SendHandlerMessage(new DisposeMessage());
        _visual = null;
        ElementComposition.SetElementChildVisual(this, null);
        base.OnDetachedFromVisualTree(e);
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_visual is not null)
            _visual.Size = new(size.Width, size.Height);
        return size;
    }

 }
