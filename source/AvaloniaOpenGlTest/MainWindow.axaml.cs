namespace AvaloniaOpenGlTest
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var content = new BackgroundControl();
            content.RenderScaling = this.RenderScaling;
            _content.Content = content;
        }
    }
}