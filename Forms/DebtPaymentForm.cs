using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class DebtPaymentForm : Form
    {
        private readonly double _remainingAmount;
        private readonly TextBox _txtAmount = new TextBox();
        private readonly ComboBox _cmbPaymentType = new ComboBox();
        private readonly TextBox _txtComment = new TextBox();

        public double AmountUZS { get; private set; }
        public string PaymentType => _cmbPaymentType.Text;
        public string CommentText => _txtComment.Text.Trim();

        public DebtPaymentForm(double remainingAmount)
        {
            _remainingAmount = remainingAmount;
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
        }

        private void InitializeComponent()
        {
            Text = "Qarz to'lovi";
            Size = new Size(430, 290);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.BodyFont;
            Padding = new Padding(8);

            Label lblRemaining = new Label
            {
                Text = $"Qoldiq: {_remainingAmount:N0} UZS",
                Left = 18,
                Top = 18,
                Width = 380,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11, FontStyle.Bold)
            };

            Controls.Add(new Label { Text = "To'lov summasi:", Left = 18, Top = 56, Width = 130 });
            _txtAmount.SetBounds(160, 52, 238, 28);
            UiTheme.StyleInput(_txtAmount);
            _txtAmount.Text = _remainingAmount.ToString("0.##", CultureInfo.InvariantCulture);

            Controls.Add(new Label { Text = "To'lov turi:", Left = 18, Top = 94, Width = 130 });
            _cmbPaymentType.SetBounds(160, 90, 238, 28);
            UiTheme.StyleInput(_cmbPaymentType);
            _cmbPaymentType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPaymentType.Items.AddRange(new object[] { "Naqd", "Karta", "Click", "Payme", "Bank o'tkazma" });
            _cmbPaymentType.SelectedIndex = 0;

            Controls.Add(new Label { Text = "Izoh:", Left = 18, Top = 132, Width = 130 });
            _txtComment.SetBounds(160, 128, 238, 56);
            _txtComment.Multiline = true;
            UiTheme.StyleInput(_txtComment);

            Button btnSave = new Button { Text = "Saqlash", Left = 238, Top = 200, Width = 74 };
            Button btnCancel = new Button { Text = "Bekor", Left = 324, Top = 200, Width = 74 };
            UiTheme.StylePrimaryButton(btnSave);
            UiTheme.StyleNeutralButton(btnCancel);
            btnSave.Click += Save_Click;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(lblRemaining);
            Controls.Add(_txtAmount);
            Controls.Add(_cmbPaymentType);
            Controls.Add(_txtComment);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
        }

        private void Save_Click(object? sender, EventArgs e)
        {
            if (!double.TryParse(_txtAmount.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double amount) &&
                !double.TryParse(_txtAmount.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
            {
                MessageBox.Show("To'lov summasi noto'g'ri.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (amount <= 0 || amount > _remainingAmount)
            {
                MessageBox.Show("To'lov summasi 0 dan katta va qoldiqdan kichik bo'lishi kerak.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AmountUZS = amount;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}


