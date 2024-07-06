
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

    record struct GlShader(int Shader);
    record struct GlProgram(int Program);
    record struct GlUniformLocation(int UniformLocation);
    record struct GlBufferObject(int Buffer, int Target, int SizeOf);
    record struct GlVertexArrayObject(int VertexArray);

    class OpenGlState : IDisposable
    {
        IGlContext          _glContext              ;
        GlBufferObject      _glVertexBufferObject   ;
        GlBufferObject      _glIndexBufferObject    ;
        GlVertexArrayObject _glVertexArrayObject    ;
        GlProgram           _glProgram              ;
        GlUniformLocation   _glTimeLocation         ;
        GlUniformLocation   _glRatioLocation        ;
        GlShader            _glVertexShader         ;
        GlShader            _glFragmentShader       ;

        static Vertex NewVertex(float x, float y, float z, float u, float v)
        {
            return new ()
            {
                Position = new (x,y,z)
            ,   TexCoord = new(u,v)
            };
        }

        const int GlPositionLocation = 0;
        const int GlTexCoordLocation = 1;

        readonly Vertex[] _vertices = 
            [
                NewVertex(-1, -1, 0, 0, 1)
            ,   NewVertex( 1, -1, 0, 1, 1)
            ,   NewVertex(-1,  1, 0, 0, 0)
            ,   NewVertex( 1,  1, 0, 1, 0)
            ]; 

        readonly ushort[] _indices = 
            [
                0
            ,   1
            ,   2
            ,   1
            ,   3
            ,   2
            ]; 

        const string _vertexShaderSource = 
            """
            #version 300 es

            precision highp float;

            in vec4 a_position;
            in vec2 a_texcoord;

            out vec2 v_texcoord;

            void main() {
              gl_Position = a_position;
              v_texcoord = a_texcoord;
            }
            """;

        const string _fragmentShaderSource = FragmentShaders.Fancy;

        public void Dispose()
        {
            var gl = _glContext.GlInterface;

            gl.BindBuffer(GL_ARRAY_BUFFER, 0);
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0);
            gl.BindVertexArray(0);
            gl.UseProgram(0);

            gl.DeleteVertexArray(_glVertexArrayObject.VertexArray);
            gl.DeleteBuffer(_glIndexBufferObject.Buffer);
            gl.DeleteBuffer(_glVertexBufferObject.Buffer);;
            gl.DeleteProgram(_glProgram.Program);
            gl.DeleteShader(_glFragmentShader.Shader);
            gl.DeleteShader(_glVertexShader.Shader);

            // _glContext.Dispose();
        }

        void CheckGlError(GlInterface gl)
        {
            var err = gl.GetError();
            if (err != GL_NO_ERROR)
            {
                throw new Exception($"GL Error state: {err}");
            }
        }
        
        GlShader CompileShader(GlInterface gl, int glEnum, string source)
        {
            var glShader = gl.CreateShader(glEnum);
            var error = gl.CompileShaderAndGetError(glShader, source);
            if (error is not null)
            {
                throw new Exception($"While compiling shader: {error}");
            }
            return new (glShader);
        }

        unsafe GlBufferObject CreateBuffer<T>(GlInterface gl, int target, T[] data)
            where T : unmanaged
        {
            var glBufferObject = gl.GenBuffer();
            gl.BindBuffer(target, glBufferObject);
            CheckGlError(gl);
            var size = sizeof(T);
            fixed (void* ptr = data)
            {
                gl.BufferData(
                        target
                    ,   data.Length*size
                    ,   new(ptr)
                    ,   GL_STATIC_DRAW
                    );
            }
            CheckGlError(gl);
            return new (glBufferObject, target, size);
        }

        public OpenGlState(IGlContext glContext)
        {
            _glContext = glContext;

            var gl = glContext.GlInterface;

            CheckGlError(gl);

            _glVertexShader     = CompileShader(gl, GL_VERTEX_SHADER, _vertexShaderSource);
            _glFragmentShader   = CompileShader(gl, GL_FRAGMENT_SHADER, _fragmentShaderSource);

            _glProgram          = new (gl.CreateProgram());

            gl.AttachShader(_glProgram.Program, _glVertexShader.Shader);
            gl.AttachShader(_glProgram.Program, _glFragmentShader.Shader);

            gl.BindAttribLocationString(_glProgram.Program, GlPositionLocation , "a_position");
            gl.BindAttribLocationString(_glProgram.Program, GlTexCoordLocation , "a_texcoord");

            var error = gl.LinkProgramAndGetError(_glProgram.Program);
            if (error is not null)
            {
                throw new Exception($"While linking shader program: {error}");
            }
            CheckGlError(gl);

            _glTimeLocation     = new (gl.GetUniformLocationString(_glProgram.Program, "time"));
            _glRatioLocation    = new (gl.GetUniformLocationString(_glProgram.Program, "ratio"));
            CheckGlError(gl);

            _glVertexBufferObject   = CreateBuffer(gl, GL_ARRAY_BUFFER          , _vertices);
            _glIndexBufferObject    = CreateBuffer(gl, GL_ELEMENT_ARRAY_BUFFER  , _indices);
            CheckGlError(gl);

            _glVertexArrayObject = new (gl.GenVertexArray());
            gl.BindVertexArray(_glVertexArrayObject.VertexArray);
            CheckGlError(gl);

            gl.VertexAttribPointer(
                    GlPositionLocation
                ,   3
                ,   GL_FLOAT
                ,   0
                ,   _glVertexBufferObject.SizeOf
                ,   0
                );
            gl.VertexAttribPointer(
                    GlTexCoordLocation
                ,   2
                ,   GL_FLOAT
                ,   0
                ,   _glVertexBufferObject.SizeOf
                ,   12
                );
            CheckGlError(gl);

            gl.EnableVertexAttribArray(GlPositionLocation);
            gl.EnableVertexAttribArray(GlTexCoordLocation);
            CheckGlError(gl);
        }

        public void DrawGl(PixelSize size, float renderScaling, float time)
        {
            var gl = _glContext.GlInterface;

            var ratio = ((float)size.Width)/((float)size.Height);

            /*
            gl.ClearColor(1, 0, 0, 1);
            gl.Clear(GL_COLOR_BUFFER_BIT);
            */

            gl.Viewport(
                    0
                ,   0
                ,   (int)(size.Width*renderScaling)
                ,   (int)(size.Height*renderScaling)
                );

            gl.BindBuffer(_glVertexBufferObject.Target, _glVertexBufferObject.Buffer);
            gl.BindBuffer(_glIndexBufferObject.Target, _glIndexBufferObject.Buffer);
            gl.BindVertexArray(_glVertexArrayObject.VertexArray);
            gl.UseProgram(_glProgram.Program);

            gl.Uniform1f(_glTimeLocation.UniformLocation, time);
            gl.Uniform1f(_glRatioLocation.UniformLocation, ratio);

            CheckGlError(gl);
            gl.DrawElements(GL_TRIANGLES, _indices.Length, GL_UNSIGNED_SHORT, IntPtr.Zero);
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
