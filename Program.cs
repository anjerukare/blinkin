using Timer = System.Windows.Forms.Timer;

namespace Blinkin
{
    /// <summary>
    /// Main application class responsible for initialization and component management
    /// </summary>
    public class Program
    {
        [STAThread]
        static void Main()
        {
            var app = new BlinkinApplication();
            app.Run();
        }
    }

    /// <summary>
    /// Main application class coordinating the multi-screen manager and system tray
    /// </summary>
    public class BlinkinApplication
    {
        private readonly MultiScreenManager _multiScreenManager;
        private readonly SystemTrayIcon _systemTrayIcon;

        public BlinkinApplication()
        {
            _multiScreenManager = new MultiScreenManager(blinkInterval: 5000, blinkDuration: 100);
            _systemTrayIcon = new SystemTrayIcon("blinkin");
        }

        /// <summary>
        /// Starts the application
        /// </summary>
        public void Run()
        {
            _systemTrayIcon.PauseResumeRequested += OnPauseResumeRequested;
            _systemTrayIcon.ExitRequested += OnExitRequested;

            Application.Run();
        }

        private void OnPauseResumeRequested(object? sender, EventArgs e)
        {
            _multiScreenManager.TogglePause();
            _systemTrayIcon.UpdatePauseResumeMenuItem(_multiScreenManager.IsPaused);
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            _multiScreenManager.Dispose();
            _systemTrayIcon.Dispose();
            Application.Exit();
        }
    }

    /// <summary>
    /// Manages blink forms across multiple screens and handles screen configuration changes
    /// </summary>
    public class MultiScreenManager : IDisposable
    {
        private readonly int _blinkInterval;
        private readonly int _blinkDuration;
        private readonly List<BlinkForm> _activeForms = [];
        private readonly Timer _screenConfigurationCheckTimer;
        private Screen[] _lastScreenConfiguration = [];
        private bool _isPaused;

        public MultiScreenManager(int blinkInterval, int blinkDuration)
        {
            _blinkInterval = blinkInterval;
            _blinkDuration = blinkDuration;

            _screenConfigurationCheckTimer = new Timer { Interval = 2000 };
            _screenConfigurationCheckTimer.Tick += CheckForScreenConfigurationChanges;
            _screenConfigurationCheckTimer.Start();

            InitializeForms();
        }

        /// <summary>
        /// Gets whether the blink reminders are currently paused
        /// </summary>
        public bool IsPaused => _isPaused;

        private void InitializeForms()
        {
            _lastScreenConfiguration = Screen.AllScreens;
            CreateFormsForAllScreens();
        }

        private void CreateFormsForAllScreens()
        {
            foreach (var screen in _lastScreenConfiguration)
            {
                CreateFormForScreen(screen);
            }
        }

        private void CreateFormForScreen(Screen screen)
        {
            var form = new BlinkForm(_blinkInterval, _blinkDuration);
            form.SetScreen(screen);
            if (!_isPaused)
            {
                form.Start();
            }
            _activeForms.Add(form);
        }

        private void CheckForScreenConfigurationChanges(object? sender, EventArgs e)
        {
            var currentScreens = Screen.AllScreens;
            if (HasScreenConfigurationChanged(currentScreens))
                RecreateForms(currentScreens);
        }

        private bool HasScreenConfigurationChanged(Screen[] currentScreens)
        {
            if (_lastScreenConfiguration.Length != currentScreens.Length)
                return true;

            for (int i = 0; i < currentScreens.Length; i++)
            {
                if (!_lastScreenConfiguration[i].Bounds.Equals(currentScreens[i].Bounds))
                    return true;
            }

            return false;
        }

        private void RecreateForms(Screen[] currentScreens)
        {
            foreach (var form in _activeForms)
            {
                form.Stop();
                form.Dispose();
            }
            _activeForms.Clear();

            _lastScreenConfiguration = currentScreens;
            CreateFormsForAllScreens();
        }

        /// <summary>
        /// Starts blink animation on all screens
        /// </summary>
        public void Start()
        {
            _isPaused = false;
            foreach (var form in _activeForms)
            {
                form.Start();
            }
        }

        /// <summary>
        /// Stops blink animation on all screens
        /// </summary>
        public void Stop()
        {
            _isPaused = true;
            foreach (var form in _activeForms)
            {
                form.Stop();
            }
        }

        /// <summary>
        /// Toggles between paused and running states across all screens
        /// </summary>
        public void TogglePause()
        {
            if (_isPaused)
                Start();
            else
                Stop();
        }

        public void Dispose()
        {
            _screenConfigurationCheckTimer?.Stop();
            _screenConfigurationCheckTimer?.Dispose();

            foreach (var form in _activeForms)
            {
                form.Stop();
                form.Dispose();
            }
            _activeForms.Clear();
        }
    }

    /// <summary>
    /// Form responsible for displaying the blinking animation
    /// </summary>
    public class BlinkForm : Form
    {
        private readonly Timer _blinkTimer;
        private readonly int _blinkDuration;
        private bool _isAnimating;
        private float _currentProgress;
        private bool _isPaused;

        /// <summary>
        /// Initializes a new instance of the BlinkForm with specified timing parameters
        /// </summary>
        /// <param name="blinkInterval">Interval between blinks in milliseconds</param>
        /// <param name="blinkDuration">Duration of the blink animation in milliseconds</param>
        public BlinkForm(int blinkInterval, int blinkDuration)
        {
            _blinkDuration = blinkDuration;
            _blinkTimer = CreateAndConfigureTimer(blinkInterval);

            InitializeForm();
        }

        private void InitializeForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            BackColor = Color.White;
            TransparencyKey = Color.White;
            ShowInTaskbar = false;
            Enabled = false;
            AllowTransparency = true;
            DoubleBuffered = true;

            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint, true);

            Paint += OnPaint;
            Hide();
        }

        private Timer CreateAndConfigureTimer(int interval)
        {
            var timer = new Timer { Interval = interval };
            timer.Tick += async (sender, e) => await ExecuteBlinkAnimation();
            return timer;
        }

        /// <summary>
        /// Sets the form location and size to match the specified screen
        /// </summary>
        /// <param name="screen">Target screen to display the form on</param>
        public void SetScreen(Screen screen)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = screen.Bounds;
        }

        /// <summary>
        /// Starts the periodic blink animation
        /// </summary>
        public void Start()
        {
            _isPaused = false;
            _blinkTimer.Start();
        }

        /// <summary>
        /// Stops the periodic blink animation
        /// </summary>
        public void Stop() => _blinkTimer.Stop();

        /// <summary>
        /// Toggles between paused and running states
        /// </summary>
        public void TogglePause()
        {
            _isPaused = !_isPaused;
            if (_isPaused)
                _blinkTimer.Stop();
            else
                _blinkTimer.Start();
        }

        /// <summary>
        /// Gets whether the blink reminders are currently paused
        /// </summary>
        public bool IsPaused => _isPaused;

        private async Task ExecuteBlinkAnimation()
        {
            if (_isAnimating || _isPaused) return;

            _isAnimating = true;
            _blinkTimer.Stop();

            Show();
            await PerformBlinkSequence();
            Hide();

            _isAnimating = false;
            if (!_isPaused)
                _blinkTimer.Start();
        }

        private async Task PerformBlinkSequence()
        {
            const int steps = 13;

            // Close eyes phase
            await AnimateBlink(steps, direction: AnimationDirection.Forward);
            await Task.Delay(100); // pause with closed eyes
            // Open eyes phase
            await AnimateBlink(steps, direction: AnimationDirection.Backward);
        }

        private async Task AnimateBlink(int steps, AnimationDirection direction)
        {
            int start = direction == AnimationDirection.Forward ? 0 : steps;
            int end = direction == AnimationDirection.Forward ? steps : 0;
            int increment = direction == AnimationDirection.Forward ? 1 : -1;

            for (int i = start; i != end + increment; i += increment)
            {
                float linearProgress = (float)i / steps;
                _currentProgress = AnimationEasing.EaseOutQuad(linearProgress);

                Invalidate();
                await Task.Delay(_blinkDuration / (steps * 2));
            }
        }

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            if (!_isAnimating)
            {
                e.Graphics.Clear(Color.Transparent);
                return;
            }

            DrawBlinkAnimation(e.Graphics);
        }

        private void DrawBlinkAnimation(Graphics graphics)
        {
            using (var brush = new SolidBrush(Color.Black))
            {
                int height = CalculateRectangleHeight();

                // Верхний прямоугольник
                graphics.FillRectangle(brush, 0, 0, Width, height);
                // Нижний прямоугольник
                int bottomY = Height - height;
                graphics.FillRectangle(brush, 0, bottomY, Width, height);
            }
        }

        private int CalculateRectangleHeight() => (int)(Height * _currentProgress / 2);

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x80000 | 0x20; // WS_EX_LAYERED | WS_EX_TRANSPARENT
                return parameters;
            }
        }
    }

    /// <summary>
    /// Manages the application's system tray icon and context menu
    /// </summary>
    public class SystemTrayIcon : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _pauseResumeMenuItem;

        public event EventHandler? ExitRequested;
        public event EventHandler? PauseResumeRequested;

        /// <summary>
        /// Initializes a new instance of the SystemTrayIcon
        /// </summary>
        /// <param name="tooltipText">Text to display when hovering over the tray icon</param>
        public SystemTrayIcon(string tooltipText)
        {
            _pauseResumeMenuItem = new ToolStripMenuItem("Pause");
            _pauseResumeMenuItem.Click += (s, e) => PauseResumeRequested?.Invoke(this, EventArgs.Empty);

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = tooltipText,
                Visible = true
            };

            InitializeContextMenu();
        }

        private void InitializeContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            var exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

            contextMenu.Items.Add(_pauseResumeMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// Updates the pause/resume menu item text based on current state
        /// </summary>
        /// <param name="isPaused">True if application is paused, false otherwise</param>
        public void UpdatePauseResumeMenuItem(bool isPaused)
        {
            _pauseResumeMenuItem.Text = isPaused ? "Resume" : "Pause";
        }

        /// <summary>
        /// Releases all resources used by the SystemTrayIcon
        /// </summary>
        public void Dispose() => _notifyIcon?.Dispose();
    }

    /// <summary>
    /// Provides easing functions for smooth animations
    /// </summary>
    public static class AnimationEasing
    {
        /// <summary>
        /// Quadratic ease-out function: fast start with smooth deceleration
        /// </summary>
        /// <param name="progress">Animation progress from 0 to 1</param>
        /// <returns>Eased progress value</returns>
        public static float EaseOutQuad(float progress) => 1 - (1 - progress) * (1 - progress);

        /// <summary>
        /// Cubic ease-out function: more pronounced deceleration
        /// </summary>
        /// <param name="progress">Animation progress from 0 to 1</param>
        /// <returns>Eased progress value</returns>
        public static float EaseOutCubic(float progress) => 1 - (float)Math.Pow(1 - progress, 3);
    }

    /// <summary>
    /// Specifies the direction of animation playback
    /// </summary>
    public enum AnimationDirection
    {
        /// <summary>
        /// Animation plays from start to end
        /// </summary>
        Forward,

        /// <summary>
        /// Animation plays from end to start
        /// </summary>
        Backward
    }
}