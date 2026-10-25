using System;
using System.Drawing;
using System.Windows.Forms;

namespace VendingMachine.UI
{
    /// <summary>
    /// Base class for the custom-drawn controls: double buffered, anti-aliased,
    /// hover / pressed tracking and keyboard activation (Enter / Space).
    /// </summary>
    internal abstract class SmoothControl : Control
    {
        protected SmoothControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable | ControlStyles.StandardClick, true);
            Surface = Theme.Panel;
            Font = Theme.Font(10f);
        }

        /// <summary>Set once the user navigates with Tab / arrow keys; cleared on the next mouse click.</summary>
        public static bool KeyboardMode { get; set; }

        /// <summary>Colour behind the control; rounded corners blend into it.</summary>
        public Color Surface { get; set; }

        protected bool Hover { get; private set; }
        protected bool Pressed { get; private set; }

        /// <summary>True when the control was focused with the keyboard (draws a focus ring).</summary>
        protected bool ShowFocusRing { get { return Focused && KeyboardMode; } }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(Surface))
                e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Gfx.Quality(e.Graphics);
            Draw(e.Graphics);
        }

        protected abstract void Draw(Graphics g);

        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Pressed = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled) { Pressed = true; KeyboardMode = false; Focus(); Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            KeyboardMode = true;
            if (Enabled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
            {
                Pressed = true;
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (Pressed && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
            {
                Pressed = false;
                Invalidate();
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData);
        }
    }
}
