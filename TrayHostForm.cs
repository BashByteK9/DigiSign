using System.Windows.Forms;

namespace DigiSign
{
    /// <summary>
    /// Invisible form that hosts the listener's tray icon message loop, giving
    /// background HTTP-handler threads a window handle to marshal calls onto
    /// (via Invoke/BeginInvoke) without ever showing a window of its own.
    /// </summary>
    internal class TrayHostForm : Form
    {
        public TrayHostForm()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Width = 0;
            Height = 0;

            // Force the native window handle to exist immediately so background threads
            // can Invoke/BeginInvoke onto this form's message loop right away - without
            // this, SetVisibleCore's override below prevents handle creation entirely.
            CreateHandle();
        }

        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);
    }
}
