using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using VendingMachine.Logic;

namespace VendingMachine.UI
{
    /// <summary>
    /// The machine itself. All rules live in <see cref="VendingMachineModel"/>;
    /// this class only translates clicks into model calls and the model state into pixels.
    /// </summary>
    internal sealed class MainForm : Form
    {
        // ---- layout (client coordinates) ----
        private static readonly Rectangle ProductsPanel = new Rectangle(28, 84, 688, 500);
        private static readonly Rectangle ControlPanel = new Rectangle(740, 84, 352, 590);

        private readonly VendingMachineModel model = new VendingMachineModel(Catalog.Create());
        private readonly Dictionary<string, Image> images = new Dictionary<string, Image>();
        private readonly List<ProductCard> cards = new List<ProductCard>();
        private readonly List<CoinButton> coinButtons = new List<CoinButton>();

        private readonly LcdDisplay lcd = new LcdDisplay();
        private readonly SegmentedToggle toggle = new SegmentedToggle("Cash", "Card");
        private readonly ReadoutBox inserted = new ReadoutBox { Caption = "Inserted" };
        private readonly ReadoutBox due = new ReadoutBox { Caption = "Still to pay" };
        private readonly ProgressStrip progress = new ProgressStrip();
        private readonly NfcPad nfc = new NfcPad();
        private readonly RoundedButton payButton = new RoundedButton { Text = "PAY" };
        private readonly RoundedButton cancelButton = new RoundedButton { Text = "Cancel and refund", Style = ButtonStyle.Secondary };
        private readonly DispenserTray tray = new DispenserTray();

        private readonly Timer transientTimer = new Timer();
        private readonly Timer cardTimer = new Timer();

        private bool hasTransient;
        private string transientTitle, transientText;
        private LcdTone transientTone;
        private bool busy;               // card authorization in progress
        private int cardStage;

        public MainForm()
        {
            Text = "Vending Machine";
            ClientSize = new Size(1120, 702);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgTop;
            DoubleBuffered = true;
            KeyPreview = true;
            Font = Theme.Font(10f);

            foreach (var p in model.Products)
                images[p.ImageKey] = ImageStore.Load(p.ImageKey);

            BuildProducts();
            BuildControlPanel();

            tray.Location = new Point(28, 598);
            tray.Surface = Gfx.Mix(Theme.BgTop, Theme.BgBottom, 0.9f);
            tray.Collected += delegate { RefreshUi(); };
            Controls.Add(tray);

            transientTimer.Tick += delegate { transientTimer.Stop(); hasTransient = false; RefreshLcd(); };
            cardTimer.Tick += OnCardTimer;

            model.Changed += delegate { RefreshUi(); };
            RefreshUi();
        }

        // ------------------------------------------------------------------ construction

        private void BuildProducts()
        {
            int drinkIndex = 0, snackIndex = 0;
            foreach (var product in model.Products)
            {
                var card = new ProductCard(product, images[product.ImageKey]);
                bool drink = product.Category == ProductCategory.Drink;
                int index = drink ? drinkIndex++ : snackIndex++;
                card.Location = new Point(50 + index * 222, drink ? 124 : 362);
                var captured = product;
                card.Click += delegate { OnProductClicked(captured); };
                cards.Add(card);
                Controls.Add(card);
            }
        }

        private void BuildControlPanel()
        {
            const int x = 764;

            lcd.Location = new Point(x, 106);
            toggle.Location = new Point(x, 244);
            toggle.SelectionChanged += delegate { OnPaymentMethodChosen(); };

            for (int i = 0; i < VendingMachineModel.CoinValues.Length; i++)
            {
                var coin = new CoinButton(VendingMachineModel.CoinValues[i]) { Location = new Point(x + i * 80, 328) };
                var value = coin.Value;
                coin.Click += delegate { OnCoinInserted(value); };
                coinButtons.Add(coin);
                Controls.Add(coin);
            }

            inserted.Location = new Point(x, 408);
            due.Location = new Point(x + 160, 408);
            progress.Location = new Point(x, 484);
            nfc.Location = new Point(x, 328);
            nfc.Click += delegate { OnCardTapped(); };

            payButton.Location = new Point(x, 516);
            payButton.Size = new Size(304, 54);
            payButton.Click += delegate { OnPayClicked(); };

            cancelButton.Location = new Point(x, 582);
            cancelButton.Size = new Size(304, 40);
            cancelButton.Click += delegate { OnCancelClicked(); };

            Controls.AddRange(new Control[] { lcd, toggle, inserted, due, progress, nfc, payButton, cancelButton });
        }

        // ------------------------------------------------------------------ user actions

        private void OnProductClicked(Product product)
        {
            if (busy) return;
            string error;
            if (!model.TrySelect(product, out error))
                ShowTransient("UNAVAILABLE", error, LcdTone.Error);
        }

        private void OnPaymentMethodChosen()
        {
            if (busy || model.Selected == null) return;
            var method = toggle.SelectedIndex == 0 ? PaymentMethod.Cash : PaymentMethod.Card;
            decimal refund = model.ChoosePaymentMethod(method);
            if (refund > 0m)
            {
                tray.AddCoins("Refund", Money.MakeChange(refund));
                ShowTransient("REFUND", Money.Format(refund) + " returned", LcdTone.Warning);
            }
        }

        private void OnCoinInserted(decimal value)
        {
            if (busy) return;
            string error;
            if (model.TryInsertCoin(value, out error))
                inserted.Flash();
            else
                ShowTransient("COIN REJECTED", error, LcdTone.Error);
        }

        private void OnPayClicked()
        {
            if (busy || model.Method != PaymentMethod.Cash || !model.IsPaid) return;

            bool donate = false;
            decimal change = model.ChangeDue;
            if (change > 0m)
            {
                using (var dialog = new DonationDialog(change))
                    donate = dialog.ShowDialog(this) == DialogResult.Yes;
            }
            CompleteSale(model.PayCash(donate));
        }

        private void OnCancelClicked()
        {
            if (busy) return;
            decimal refund = model.Cancel();
            if (refund > 0m)
            {
                tray.AddCoins("Refund", Money.MakeChange(refund));
                ShowTransient("CANCELLED", Money.Format(refund) + " returned", LcdTone.Warning);
            }
            else
            {
                ShowTransient("CANCELLED", "Nothing was charged", LcdTone.Warning);
            }
        }

        private void OnCardTapped()
        {
            if (busy || model.Method != PaymentMethod.Card || nfc.State != NfcState.Idle) return;

            busy = true;
            cardStage = 1;
            nfc.State = NfcState.Processing;
            cardTimer.Interval = 1300;
            cardTimer.Start();
            RefreshUi();
        }

        private void OnCardTimer(object sender, EventArgs e)
        {
            cardTimer.Stop();
            if (cardStage == 1)
            {
                cardStage = 2;
                nfc.State = NfcState.Approved;
                RefreshLcd();
                cardTimer.Interval = 800;
                cardTimer.Start();
            }
            else
            {
                busy = false;
                nfc.State = NfcState.Idle;
                CompleteSale(model.PayCard());
            }
        }

        private void CompleteSale(SaleResult sale)
        {
            string name = sale.Product.Name;
            tray.Drop(images[sale.Product.ImageKey], name + "  ·  " + sale.Product.Code);
            if (sale.ChangeCoins.Count > 0)
                tray.AddCoins("Change", sale.ChangeCoins);

            string info;
            if (sale.Method == PaymentMethod.Card) info = "Paid by card";
            else if (sale.ChangeDonated) info = "Change of " + Money.Format(sale.Change) + " donated. Thank you!";
            else if (sale.Change > 0m) info = "Take your change: " + Money.Format(sale.Change);
            else info = "Exact amount. Thank you!";

            ShowTransient("ENJOY YOUR " + name.ToUpperInvariant() + "!", info, LcdTone.Success, 5000);
            RefreshUi();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab || keyData == (Keys.Tab | Keys.Shift)) SmoothControl.KeyboardMode = true;
            if (keyData == Keys.Escape && cancelButton.Enabled)
            {
                OnCancelClicked();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ model -> UI

        private void RefreshUi()
        {
            var selected = model.Selected;
            bool cardMode = model.Method == PaymentMethod.Card;
            bool cashMode = model.Method == PaymentMethod.Cash;

            foreach (var card in cards)
            {
                card.Selected = card.Product == selected;
                card.Enabled = !busy;
                card.Invalidate();   // stock may have changed
            }

            toggle.Enabled = selected != null && !busy;
            toggle.SelectedIndex = cashMode ? 0 : cardMode ? 1 : -1;

            foreach (var coin in coinButtons)
            {
                coin.Visible = !cardMode;
                coin.Enabled = selected != null && cashMode && !busy;
            }
            inserted.Visible = due.Visible = progress.Visible = !cardMode;
            nfc.Visible = cardMode;
            if (!cardMode && !busy) nfc.State = NfcState.Idle;

            inserted.Enabled = due.Enabled = selected != null && cashMode;
            inserted.Value = Money.Format(model.Credit);
            inserted.ValueColor = Theme.Text;
            due.Value = Money.Format(model.Due);
            due.ValueColor = model.IsPaid ? Theme.Success : Theme.Amber;
            progress.Progress = selected == null || !cashMode ? 0f : (float)(model.Credit / selected.Price);

            if (cardMode)
            {
                payButton.Text = "TAP THE READER TO PAY";
                payButton.Enabled = false;
            }
            else
            {
                payButton.Text = selected == null ? "PAY" : "PAY " + Money.Format(selected.Price);
                payButton.Enabled = model.IsPaid && !busy;
            }
            payButton.Pulse = payButton.Enabled;
            cancelButton.Enabled = (selected != null || model.Credit > 0m) && !busy;

            Invalidate(new Rectangle(ControlPanel.X, 200, ControlPanel.Width, 140));   // dynamic captions
            RefreshLcd();
        }

        private void ShowTransient(string title, string text, LcdTone tone, int milliseconds = 2400)
        {
            hasTransient = true;
            transientTitle = title;
            transientText = text;
            transientTone = tone;
            transientTimer.Stop();
            transientTimer.Interval = milliseconds;
            transientTimer.Start();
            RefreshLcd();
        }

        private void RefreshLcd()
        {
            if (hasTransient)
            {
                lcd.Show(transientTitle, transientText, transientTone);
                return;
            }

            var p = model.Selected;
            if (busy)
            {
                if (cardStage == 1) lcd.Show("CARD PAYMENT", "Authorizing…", LcdTone.Warning);
                else lcd.Show("CARD PAYMENT", "Approved", LcdTone.Success);
            }
            else if (p == null)
                lcd.Show("WELCOME", "Select a product", LcdTone.Normal);
            else if (model.Method == PaymentMethod.None)
                lcd.Show(p.Name + "  ·  " + Money.Format(p.Price), "Choose cash or card", LcdTone.Normal);
            else if (model.Method == PaymentMethod.Card)
                lcd.Show(p.Name + "  ·  " + Money.Format(p.Price), "Tap your card", LcdTone.Normal);
            else if (model.IsPaid)
                lcd.Show(p.Name + "  ·  " + Money.Format(p.Price),
                    model.ChangeDue > 0m ? "Press PAY\nChange: " + Money.Format(model.ChangeDue) : "Press PAY", LcdTone.Success);
            else
                lcd.Show(p.Name + "  ·  " + Money.Format(p.Price),
                    model.Credit == 0m ? "Insert coins" : "Insert " + Money.Format(model.Due) + " more", LcdTone.Normal);
        }

        // ------------------------------------------------------------------ painting

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var lg = new LinearGradientBrush(ClientRectangle, Theme.BgTop, Theme.BgBottom, 90f))
                e.Graphics.FillRectangle(lg, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            DrawHeader(g);
            DrawPanel(g, ProductsPanel);
            DrawPanel(g, ControlPanel);

            DrawCaption(g, "DRINKS", 50, 102, 666);
            DrawCaption(g, "SNACKS", 50, 340, 666);

            DrawCaption(g, "PAYMENT METHOD", 764, 220, 1068);
            if (model.Method == PaymentMethod.Card) DrawCaption(g, "CONTACTLESS", 764, 306, 1068);
            else DrawCaption(g, "INSERT COINS", 764, 306, 1068);

            Gfx.Text(g, "Coins: 0.50 · 1 · 2 · 5 PLN   |   Max credit " + Money.Format(VendingMachineModel.MaxCredit),
                Theme.Font(8f), Theme.Muted, new RectangleF(764, 636, 304, 22), StringAlignment.Center);
        }

        private static void DrawHeader(Graphics g)
        {
            var logo = new RectangleF(30, 16, 44, 44);
            using (var path = Gfx.Round(logo, 12))
            using (var lg = new LinearGradientBrush(logo, Theme.Accent, Theme.AccentDark, 45f))
                g.FillPath(lg, path);

            // tiny vending-machine glyph
            using (var pen = new Pen(Color.FromArgb(4, 40, 36), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawRectangle(pen, 41, 25, 22, 26);
                g.DrawLine(pen, 46, 33, 58, 33);
                g.DrawLine(pen, 46, 39, 58, 39);
                g.DrawLine(pen, 46, 45, 52, 45);
            }

            Gfx.Text(g, "VENDING MACHINE", Theme.Font(19f, FontStyle.Bold), Theme.Text, new RectangleF(90, 12, 500, 34));
            Gfx.Text(g, "Pick a product   ·   Pay with cash or card   ·   Enjoy", Theme.Font(9.5f), Theme.Muted, new RectangleF(91, 44, 600, 20));

            using (var b = new SolidBrush(Theme.Success)) g.FillEllipse(b, 986, 33, 9, 9);
            Gfx.Text(g, "READY", Theme.Font(9f, FontStyle.Bold), Theme.Muted, new RectangleF(1002, 28, 90, 20));
        }

        private static void DrawPanel(Graphics g, Rectangle r)
        {
            for (int i = 6; i >= 1; i--)   // soft drop shadow
                Gfx.FillRound(g, Color.FromArgb(10, 0, 0, 0), new RectangleF(r.X - i + 1, r.Y - i + 7, r.Width + 2 * i - 2, r.Height + 2 * i - 2), 22 + i);
            Gfx.FillRound(g, Theme.Panel, r, 22);
            Gfx.DrawRound(g, Theme.Line, 1f, r, 22);
        }

        private static void DrawCaption(Graphics g, string text, float x, float y, float right)
        {
            var font = Theme.Font(8.5f, FontStyle.Bold);
            SizeF size = g.MeasureString(text, font);
            Gfx.Text(g, text, font, Theme.Muted, new RectangleF(x, y, size.Width + 6, 18));
            using (var pen = new Pen(Theme.Line, 1f))
                g.DrawLine(pen, x + size.Width + 12, y + 9, right, y + 9);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                transientTimer.Dispose();
                cardTimer.Dispose();
                foreach (var image in images.Values) image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
