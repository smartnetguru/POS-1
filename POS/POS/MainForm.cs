﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using POS.Internals;
using POS.Internals.Designer;
using POS.Internals.FilterBuilder;
using POS.Internals.UndoRedo;
using POS.Models;
using POS.Properties;
using Telerik.WinControls.UI;

namespace POS
{
    public partial class MainForm : Telerik.WinControls.UI.RadForm
    {
        private DesignerHost host;

        public MainForm()
        {
            this.InitializeComponent();
            
            var f = new Panel();
            f.BorderStyle = BorderStyle.FixedSingle;
            f.Visible = true;

            var undoBtn = new RadButtonElement();
            undoBtn.Image = Resources.Undo_icon;
            undoBtn.Name = "undoBtn";
            undoBtn.ShowBorder = false;
            undoBtn.Enabled = false;
            undoBtn.Click += (s, e) => UndoRedoManager.Undo();

            var redoBtn = new RadButtonElement();
            redoBtn.Image = Resources.Redo_icon;
            redoBtn.Name = "redoBtn";
            redoBtn.ShowBorder = false;
            redoBtn.Enabled = false;
            redoBtn.Click += (s, e) => UndoRedoManager.Redo();

            UndoRedoManager.CommandDone += (s, e) =>
            {
                undoBtn.Enabled = UndoRedoManager.CanUndo;
                redoBtn.Enabled = UndoRedoManager.CanRedo;
            };

            this.FormElement.TitleBar.SystemButtons.Children.Insert(0, undoBtn);
            this.FormElement.TitleBar.SystemButtons.Children.Insert(1, redoBtn);

            this.host = DesignerHost.CreateHost(f, null, (sender, e) => { this.propertyGrid.SelectedObject = sender; }, false);

            this.host.AddControl(new Button());

            this.radPanel1.Controls.Add(this.host);

            this.ProductsView.Groups.Clear();

            Price.PriceChanged += (s, e) =>
            {
                this.priceLbl.DigitText = Price.Value; 
                this.historyView1.Invalidate();
            };

            this.productsView1.DataSource = ServiceLocator.Products;
            this.productsView1.BestFitColumns();

            foreach (var pc in ServiceLocator.ProductCategories)
            {
                var tmp = new TileGroupElement() { Name = pc.Name, Visibility = Telerik.WinControls.ElementVisibility.Visible, Text = pc.Name, Tag = pc.id };

                foreach (var p in ServiceLocator.Products)
                {
                    var tmpItem = new RadTileElement() { Text = p.ID, Image = Image.FromStream(new MemoryStream(p.Image)), Name = p.ID, Tag = p.TotalPrice, Visibility = Telerik.WinControls.ElementVisibility.Visible };

                    tmpItem.Click += (s, e) =>
                    {
                        Price.Add(p);
                        ServiceLocator.ProductHistory.Add(p);
                    };

                    tmp.Items.Add(tmpItem);
                }

                this.ProductsView.Groups.Add(tmp);
            }

            #if DEBUG
            Cursor.Show();
            #else
            Cursor.Hide();
            #endif

            PluginLoader.AddObject("add_pay_button", new Action<Button>(AddPayButton));
        }

        public void AddPayButton(Button btn) {
            btn.Dock = DockStyle.Right;

            payFooter.Controls.Add(btn);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }

        private void PropertiesBtn_Click(object sender, EventArgs e)
        {
            this.propertyGrid.Visible = !this.propertyGrid.Visible;
        }

        private void ImageBtn_Click(object sender, EventArgs e)
        {
            var fb = new FilterBuilder();
            fb.Add(FilterBuilder.Filters.AllImageFiles);

            OpenFileDialog f = new OpenFileDialog();
            f.Filter = fb.ToString();

            if (f.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var img = Image.FromFile(f.FileName);
                var pb = new PictureBox();
                pb.Image = img;

                this.host.AddControl(pb);
            }
        }

        private void radButton1_Click(object sender, EventArgs e)
        {
            Price.RemoveLast();
            ServiceLocator.ProductHistory.RemoveLast();
        }

        private void paywithEuroBtn_Click(object sender, EventArgs e)
        {
            var inv = Invoice.New();
            inv.Products = new List<Product>(ServiceLocator.Products);
            inv.TotalPrice = Price.NumberValue;
            inv.Currency = Invoice.InvoiceCurrency.EUR;

            var i = new List<Invoice>();
            i.Add(inv);

            var sWindow = WindowManager.GetStatusWindow();
            if (sWindow != null)
            {
                var ctrl = new Label();
                ctrl.Text = "Danke für Ihren Einkauf";
                ctrl.Font = new Font("Arial", 24);

                sWindow.SetDisplay(ctrl);
            }

            ServiceLocator.Invoices = i.ToArray();

            ServiceLocator.ProductHistory.Clear();
            Price.Clear();
        }

        private async void paywithBTCBtn_Click(object sender, EventArgs e)
        {
            var inv = Invoice.New();
            inv.Products = new List<Product>(ServiceLocator.Products);
            inv.TotalPrice = Price.NumberValue;
            inv.Currency = Invoice.InvoiceCurrency.BTC;

            var i = new List<Invoice>();
            i.Add(inv);

            var addr = Info.Blockchain.API.Receive.Receive.ReceiveFunds("1HaWUwi5oYQo2RXB5k5JLgJA8MTigNuLeY", "").DestinationAddress;
            var btc = Info.Blockchain.API.ExchangeRates.ExchangeRates.ToBTC("EUR", Price.NumberValue);

            QrCodeDialog.Show(string.Format("bitcoin:{0}?{1}", addr, btc), null);

            if (await BitcoinPayer.CheckPaymentAsync(addr, btc))
            {
                this.notifyIcon1.ShowBalloonTip(5000, "Bitcoin", "Betrag wurde gezahlt", ToolTipIcon.Info);

                ServiceLocator.Invoices = i.ToArray();

                ServiceLocator.ProductHistory.Clear();
                Price.Clear();
            }
            else
            {
                this.notifyIcon1.ShowBalloonTip(5000, "Bitcoin", "Betrag wurde nicht gezahlt", ToolTipIcon.Error);
            }
        }

        private void btcddressTb_TextChanged(object sender, EventArgs e)
        {
            Settings.Set("bitcoinaddress", "1HaWUwi5oYQo2RXB5k5JLgJA8MTigNuLeY"); //btcddressTb.Text);
        }
    }
}