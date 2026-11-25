using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using StatsTool.Data;

namespace StatsTool.Views
{
    public partial class DailyEntryView : UserControl
    {
        // =====================================================
        // PRODUCT CATEGORY AND SELLING UNIT RULES
        // =====================================================
        //
        // FRONTEND:
        // Selecting a category filters the available selling units.
        //
        // BACKEND TEAM:
        // Validate the category/unit combination independently.
        // Do not trust the frontend dropdown as the only check.
        //
        // DATABASE TEAM:
        // Use stable identifiers for categories and units.
        // Keep these options configurable for different stores.
        //
        // Selling units describe how quantities are recorded.
        // No automatic unit conversion is performed.
        //
        // =====================================================

        private static readonly Dictionary<string, string[]>
            CategoryUnits = new()
            {
                ["Beverages"] = new[]
                {
                    "Bottle (250 mL)",
                    "Bottle (500 mL)",
                    "Bottle (1 L)",
                    "Can (330 mL)",
                    "Can (500 mL)",
                    "Carton (1 L)",
                    "Milliliter (mL)",
                    "Liter (L)"
                },

                ["Cooking Oils & Liquids"] = new[]
                {
                    "Bottle (250 mL)",
                    "Bottle (500 mL)",
                    "Bottle (1 L)",
                    "Can (1 L)",
                    "Milliliter (mL)",
                    "Liter (L)"
                },

                ["Grains & Dry Goods"] = new[]
                {
                    "Gram (g)",
                    "Kilogram (kg)",
                    "Bag (1 kg)",
                    "Bag (5 kg)",
                    "Sack (25 kg)",
                    "Pack"
                },

                ["Packaged Food & Snacks"] = new[]
                {
                    "Piece",
                    "Pack",
                    "Box",
                    "Can",
                    "Sachet",
                    "Bag"
                },

                ["Fresh Produce"] = new[]
                {
                    "Piece",
                    "Bunch",
                    "Gram (g)",
                    "Kilogram (kg)",
                    "Pack",
                    "Crate"
                },

                ["Meat & Seafood"] = new[]
                {
                    "Gram (g)",
                    "Kilogram (kg)",
                    "Pack",
                    "Tray"
                },

                ["Clothing & Footwear"] = new[]
                {
                    "Piece",
                    "Pair",
                    "Set",
                    "Pack"
                },

                ["Construction Materials"] = new[]
                {
                    "Piece",
                    "Meter (m)",
                    "Kilogram (kg)",
                    "Bag",
                    "Sack",
                    "Roll"
                },

                ["Hardware & Tools"] = new[]
                {
                    "Piece",
                    "Set",
                    "Box",
                    "Meter (m)",
                    "Roll"
                },

                ["Household & Cleaning"] = new[]
                {
                    "Piece",
                    "Pack",
                    "Box",
                    "Bottle (500 mL)",
                    "Bottle (1 L)",
                    "Liter (L)"
                },

                ["Personal Care"] = new[]
                {
                    "Piece",
                    "Pack",
                    "Sachet",
                    "Tube",
                    "Bottle (100 mL)",
                    "Bottle (500 mL)"
                },

                ["Electronics & Accessories"] = new[]
                {
                    "Piece",
                    "Unit",
                    "Set",
                    "Box"
                },

                ["Stationery & Office"] = new[]
                {
                    "Piece",
                    "Pack",
                    "Box",
                    "Ream",
                    "Set"
                },

                ["Textiles & Fabric"] = new[]
                {
                    "Meter (m)",
                    "Yard (yd)",
                    "Roll",
                    "Piece"
                },

                ["Other / General Merchandise"] = new[]
                {
                    "Piece",
                    "Pack",
                    "Box",
                    "Pair",
                    "Set",
                    "Gram (g)",
                    "Kilogram (kg)",
                    "Milliliter (mL)",
                    "Liter (L)",
                    "Meter (m)"
                }
            };

        // =====================================================
        // FRONTEND STATE
        // =====================================================

        private readonly List<DailyEntryProduct> _products = new();

        private DailyEntryProduct? _editingProduct;

        private string? _selectedBatchFile;

        private bool _settingUpEditor;

        private DateTime _previewWeekStart =>
            GetWeekStart(ReportingDatePicker.SelectedDate ?? DateTime.Today);

        private readonly Brush _navyBrush =
            new SolidColorBrush(Color.FromRgb(11, 21, 48));

        public DailyEntryView()
        {
            InitializeComponent();

            ReportingDatePicker.SelectedDate = DateTime.Today;
            LoadDatabaseProducts();
            RefreshProductPicker();
            QuantitySoldInput.Text = "0";
            AvailableStockInput.Text = SelectedProduct?.CurrentStock.ToString() ?? "0";
            PreviewWeekText.Text = FormatWeek(GetWeekStart(DateTime.Today));
        }

        private void LoadDatabaseProducts()
        {
            try
            {
                if (MainWindow.CurrentUserId == null) return;
                DateTime week = GetWeekStart(ReportingDatePicker.SelectedDate ?? DateTime.Today);
                var records = DatabaseService.GetProducts(MainWindow.CurrentUserId, week);
                _products.Clear();

                foreach (var item in records)
                {
                    _products.Add(new DailyEntryProduct
                    {
                        DatabaseId = item.Id,
                        ProductId = Guid.NewGuid(),
                        Name = item.Name,
                        DateAdded = item.DateAdded,
                        DisplayName = item.Name + " — " + item.Unit,
                        Category = item.Category,
                        SellingUnit = item.Unit,
                        UnitLabel = GetUnitLabel(item.Unit),
                        WeeklyUnits = item.CurrentWeekUnits,
                        CurrentStock = item.CurrentStock
                    });
                }
            }
            catch (Exception ex)
            {
                ShowFeedback("Could not load products: " + ex.Message, true);
            }
        }

        // =====================================================
        // PAGE LOADING AND RESPONSIVENESS
        // =====================================================

        private void DailyEntryView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            LoadDatabaseProducts();
            RefreshProductPicker();
            UpdateResponsiveLayout(ActualWidth);
            UpdateSelectedProductDetails();
            PreviewCurrentEntry();
        }

        private void DailyEntryView_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout(e.NewSize.Width);
        }

        private void UpdateResponsiveLayout(double width)
        {
            if (MainPanels == null)
                return;

            // Desktop: side-by-side panels.
            // Narrow windows: stacked panels.
            // Matches Dashboard's proven breakpoint.

            MainPanels.Columns = width >= 850 ? 2 : 1;
        }

        // =====================================================
        // PRODUCT SELECTION
        // =====================================================

        private DailyEntryProduct? SelectedProduct =>
            ProductComboBox.SelectedItem as DailyEntryProduct;

        private void RefreshProductPicker()
        {
            DailyEntryProduct? previous =
                ProductComboBox.SelectedItem as DailyEntryProduct;

            List<DailyEntryProduct> active = _products.ToList();

            ProductComboBox.ItemsSource = active;

            if (previous != null && active.Contains(previous))
            {
                ProductComboBox.SelectedItem = previous;
            }
            else if (active.Count > 0)
            {
                ProductComboBox.SelectedIndex = 0;
            }
        }

        private void ProductComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            DailyEntryProduct? product = SelectedProduct;

            if (product != null)
            {
                QuantitySoldInput.Text = "0";

                AvailableStockInput.Text =
                    product.CurrentStock.ToString();
            }

            UpdateSelectedProductDetails();
            PreviewCurrentEntry();
        }

        private void UpdateSelectedProductDetails()
        {
            DailyEntryProduct? product = SelectedProduct;

            CategoryValueText.Text =
                product?.Category ?? "—";

            SellingUnitValueText.Text =
                product?.SellingUnit ?? "—";

            SoldUnitText.Text =
                product?.UnitLabel ?? "";

            StockUnitText.Text =
                product?.UnitLabel ?? "";
        }

        // =====================================================
        // REPORTING WEEK
        // =====================================================

        private void ReportingDatePicker_SelectedDateChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                LoadDatabaseProducts();
                RefreshProductPicker();
                UpdateSelectedProductDetails();
                PreviewCurrentEntry();
            }
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            int daysSinceMonday =
                ((int)date.DayOfWeek + 6) % 7;

            return date.Date.AddDays(-daysSinceMonday);
        }

        private static string FormatWeek(DateTime monday)
        {
            DateTime sunday = monday.AddDays(6);

            if (monday.Month == sunday.Month &&
                monday.Year == sunday.Year)
            {
                return $"{monday:MMM d}–{sunday.Day}, {sunday:yyyy}";
            }

            return $"{monday:MMM d}–{sunday:MMM d, yyyy}";
        }

        // =====================================================
        // INPUT VALIDATION
        // =====================================================

        private bool TryReadEntry(
            out DailyEntryProduct? product,
            out int quantity,
            out int availableStock)
        {
            product = SelectedProduct;
            quantity = 0;
            availableStock = 0;

            if (product == null)
            {
                ShowFeedback("Please select a product.", true);
                return false;
            }

            if (ReportingDatePicker.SelectedDate == null)
            {
                ShowFeedback("Please select a reporting date.", true);
                return false;
            }

            if (!int.TryParse(
                    QuantitySoldInput.Text.Trim(),
                    out quantity) ||
                quantity < 0)
            {
                ShowFeedback(
                    "Quantity sold must be a non-negative whole number.",
                    true);

                return false;
            }

            if (!int.TryParse(
                    AvailableStockInput.Text.Trim(),
                    out availableStock) ||
                availableStock < 0)
            {
                ShowFeedback(
                    "Available stock must be a non-negative whole number.",
                    true);

                return false;
            }

            return true;
        }

        // =====================================================
        // PREVIEW CHANGES
        // =====================================================

        private void PreviewChangesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            PreviewCurrentEntry();
        }

        private void PreviewCurrentEntry()
        {
            if (!TryReadEntry(
                    out DailyEntryProduct? product,
                    out int quantity,
                    out _))
            {
                return;
            }

            if (product == null)
                return;

            DateTime selectedWeek = GetWeekStart(
                ReportingDatePicker.SelectedDate!.Value);

            if (selectedWeek != _previewWeekStart)
            {
                RenderPreview(product, 0);

                ShowFeedback(
                    "The selected date is outside the displayed week. " +
                    "The current-week preview remains unchanged.",
                    false);

                return;
            }

            EntryFeedbackText.Visibility =
                Visibility.Collapsed;

            RenderPreview(product, quantity);
        }

        private void RenderPreview(
            DailyEntryProduct product,
            int pendingQuantity)
        {
            int weeklyBefore =
                _products.Sum(p => p.WeeklyUnits);

            int weeklyAfter =
                weeklyBefore + pendingQuantity;

            int productBefore =
                product.WeeklyUnits;

            int productAfter =
                productBefore + pendingQuantity;

            int productsBefore =
                _products.Count(p => p.WeeklyUnits > 0);

            int productsAfter =
                _products.Count(p =>
                    p.WeeklyUnits > 0 ||
                    (p == product && pendingQuantity > 0));

            WeeklyUnitsBeforeText.Text =
                weeklyBefore.ToString();

            WeeklyUnitsAfterText.Text =
                weeklyAfter.ToString();

            WeeklyDeltaText.Text =
                pendingQuantity > 0
                    ? $"+{pendingQuantity}"
                    : "";

            PreviewProductNameText.Text =
                product.Name;

            ProductUnitsBeforeText.Text =
                productBefore.ToString();

            ProductUnitsAfterText.Text =
                productAfter.ToString();

            ProductDeltaText.Text =
                pendingQuantity > 0
                    ? $"+{pendingQuantity}"
                    : "";

            ProductsBeforeText.Text =
                productsBefore.ToString();

            ProductsAfterText.Text =
                productsAfter.ToString();

            PreviewStatusText.Text =
                pendingQuantity > 0
                    ? "Unsaved Preview"
                    : "No Pending Changes";

            DrawWeeklyProductChart(
                product,
                pendingQuantity);
        }

        // =====================================================
        // SAVE DAILY ENTRY
        // =====================================================

        private void SaveEntryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!TryReadEntry(out DailyEntryProduct? product,
                    out int quantity, out int closingStock) || product == null)
                return;

            try
            {
                if (MainWindow.CurrentUserId == null)
                    throw new InvalidOperationException("No signed-in account.");

                DateTime date = ReportingDatePicker.SelectedDate!.Value.Date;
                DatabaseService.SaveDailySale(
                    MainWindow.CurrentUserId, product.DatabaseId,
                    date, quantity, closingStock);

                LoadDatabaseProducts();
                RefreshProductPicker();
                ProductComboBox.SelectedItem =
                    _products.FirstOrDefault(x => x.DatabaseId == product.DatabaseId);
                UpdateSelectedProductDetails();
                QuantitySoldInput.Text = "0";
                AvailableStockInput.Text = SelectedProduct?.CurrentStock.ToString() ?? closingStock.ToString();
                PreviewCurrentEntry();
                ShowFeedback("Daily sales record saved to SQL Server.", false);
            }
            catch (Exception ex)
            {
                ShowFeedback("Could not save daily record: " + ex.Message, true);
            }
        }

        // =====================================================
        // BATCH IMPORT
        // =====================================================

        private void LoadBatchFileButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select daily sales CSV",
                Filter = "CSV Files (*.csv)|*.csv",
                Multiselect = false,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                if (MainWindow.CurrentUserId == null)
                    throw new InvalidOperationException("No signed-in account.");

                string[] lines = System.IO.File.ReadAllLines(dialog.FileName);
                if (lines.Length < 2)
                    throw new InvalidOperationException("The CSV has no data rows.");

                string[] headers = lines[0].Split(',')
                    .Select(x => x.Trim().Trim('"')).ToArray();

                int idColumn = Array.FindIndex(headers, x => x.Equals("I_ID", StringComparison.OrdinalIgnoreCase));
                int dateColumn = Array.FindIndex(headers, x => x.Equals("S_Date", StringComparison.OrdinalIgnoreCase));
                int quantityColumn = Array.FindIndex(headers, x => x.Equals("S_Quantity", StringComparison.OrdinalIgnoreCase));
                int stockColumn = Array.FindIndex(headers, x => x.Equals("S_DailyStock", StringComparison.OrdinalIgnoreCase));

                if (new[] { idColumn, dateColumn, quantityColumn, stockColumn }.Any(x => x < 0))
                    throw new InvalidOperationException("CSV headers must be: I_ID,S_Date,S_Quantity,S_DailyStock.");

                var parsedRows = new List<(string Id, DateTime Date, int Quantity, int Stock)>();
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    string[] values = lines[i].Split(',')
                        .Select(x => x.Trim().Trim('"')).ToArray();
                    int max = new[] { idColumn, dateColumn, quantityColumn, stockColumn }.Max();
                    if (values.Length <= max)
                        throw new InvalidOperationException($"Invalid CSV row {i + 1}.");

                    if (!DateTime.TryParse(values[dateColumn], CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTime date) ||
                        !int.TryParse(values[quantityColumn], out int quantity) || quantity < 0 ||
                        !int.TryParse(values[stockColumn], out int stock) || stock < 0 ||
                        string.IsNullOrWhiteSpace(values[idColumn]))
                        throw new InvalidOperationException($"Invalid values on CSV row {i + 1}.");

                    parsedRows.Add((values[idColumn], date, quantity, stock));
                }

                foreach (var row in parsedRows)
                    DatabaseService.SaveDailySale(MainWindow.CurrentUserId,
                        row.Id, row.Date, row.Quantity, row.Stock);

                _selectedBatchFile = dialog.FileName;
                BatchFileInfoText.Text = $"Imported {parsedRows.Count} daily record(s): " +
                    System.IO.Path.GetFileName(dialog.FileName);
                BatchFileInfoText.Visibility = Visibility.Visible;

                LoadDatabaseProducts();
                RefreshProductPicker();
                UpdateSelectedProductDetails();
                PreviewCurrentEntry();
                ShowFeedback($"{parsedRows.Count} daily record(s) imported into SQL Server.", false);
            }
            catch (Exception ex)
            {
                ShowFeedback("CSV import failed: " + ex.Message, true);
            }
        }

        // =====================================================
        // PRODUCT EDITOR - DROPDOWN CONFIGURATION
        // =====================================================

        private void ConfigureProductEditor(
            string? category,
            string? sellingUnit)
        {
            _settingUpEditor = true;

            EditorCategoryComboBox.ItemsSource =
                CategoryUnits.Keys.ToList();

            EditorCategoryComboBox.SelectedItem = category;

            PopulateUnitDropdown(sellingUnit);

            _settingUpEditor = false;
        }

        private void EditorCategoryComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_settingUpEditor)
                return;

            // When category changes, previous unit selections
            // are cleared to prevent invalid combinations.

            PopulateUnitDropdown(null);
        }

        private void PopulateUnitDropdown(
            string? preferredUnit)
        {
            string? category =
                EditorCategoryComboBox.SelectedItem as string;

            if (category == null ||
                !CategoryUnits.TryGetValue(
                    category,
                    out string[]? units))
            {
                EditorUnitComboBox.ItemsSource = null;
                EditorUnitComboBox.SelectedItem = null;
                EditorUnitComboBox.IsEnabled = false;
                return;
            }

            EditorUnitComboBox.ItemsSource = units;
            EditorUnitComboBox.IsEnabled = true;

            if (preferredUnit != null &&
                units.Contains(preferredUnit))
            {
                EditorUnitComboBox.SelectedItem = preferredUnit;
            }
            else
            {
                EditorUnitComboBox.SelectedItem = null;
            }
        }

        // =====================================================
        // ADD PRODUCT
        // =====================================================

        private void AddProductButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _editingProduct = null;

            ProductEditorTitle.Text = "Add Product";

            EditorNameInput.Text = "";

            ConfigureProductEditor(null, null);

            ProductEditorErrorText.Visibility =
                Visibility.Collapsed;

            ProductEditorOverlay.Visibility =
                Visibility.Visible;

            EditorNameInput.Focus();
        }

        // =====================================================
        // EDIT PRODUCT
        // =====================================================

        private void EditProductButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            DailyEntryProduct? product = SelectedProduct;

            if (product == null)
                return;

            _editingProduct = product;

            ProductEditorTitle.Text = "Edit Product";

            EditorNameInput.Text = product.Name;

            ConfigureProductEditor(
                product.Category,
                product.SellingUnit);

            ProductEditorErrorText.Visibility =
                Visibility.Collapsed;

            ProductEditorOverlay.Visibility =
                Visibility.Visible;

            EditorNameInput.Focus();
        }

        // =====================================================
        // SAVE PRODUCT
        // =====================================================

        private void SaveProductEditorButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string name = EditorNameInput.Text.Trim();
            string? category = EditorCategoryComboBox.SelectedItem as string;
            string? unit = EditorUnitComboBox.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(category) ||
                string.IsNullOrWhiteSpace(unit))
            {
                ProductEditorErrorText.Text = "Enter a product name, category, and selling unit.";
                ProductEditorErrorText.Visibility = Visibility.Visible;
                return;
            }

            bool duplicate = _products.Any(p =>
                p.DatabaseId != _editingProduct?.DatabaseId &&
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                ProductEditorErrorText.Text = "A product with this name already exists.";
                ProductEditorErrorText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                if (MainWindow.CurrentUserId == null)
                    throw new InvalidOperationException("No signed-in account.");

                DatabaseService.SaveProduct(
                    MainWindow.CurrentUserId,
                    _editingProduct?.DatabaseId,
                    name, category, unit,
                    _editingProduct?.DateAdded ?? DateTime.Today);

                ProductEditorOverlay.Visibility = Visibility.Collapsed;
                string? selectedId = _editingProduct?.DatabaseId;
                _editingProduct = null;
                LoadDatabaseProducts();
                RefreshProductPicker();
                if (selectedId != null)
                    ProductComboBox.SelectedItem = _products.FirstOrDefault(x => x.DatabaseId == selectedId);
                else
                    ProductComboBox.SelectedItem = _products.FirstOrDefault(x => x.Name == name);
                UpdateSelectedProductDetails();
                PreviewCurrentEntry();
            }
            catch (Exception ex)
            {
                ProductEditorErrorText.Text = ex.Message;
                ProductEditorErrorText.Visibility = Visibility.Visible;
            }
        }

        // =====================================================
        // UNIT LABELS FOR THE DAILY ENTRY FORM
        // =====================================================

        private static string GetUnitLabel(string unit)
        {
            if (unit.StartsWith(
                    "Bottle",
                    StringComparison.OrdinalIgnoreCase))
                return "bottles";

            if (unit.StartsWith(
                    "Bag",
                    StringComparison.OrdinalIgnoreCase))
                return "bags";

            if (unit.StartsWith(
                    "Can",
                    StringComparison.OrdinalIgnoreCase))
                return "cans";

            if (unit.StartsWith(
                    "Pack",
                    StringComparison.OrdinalIgnoreCase))
                return "packs";

            if (unit.StartsWith(
                    "Piece",
                    StringComparison.OrdinalIgnoreCase))
                return "pieces";

            if (unit.StartsWith(
                    "Pair",
                    StringComparison.OrdinalIgnoreCase))
                return "pairs";

            if (unit.StartsWith(
                    "Kilogram",
                    StringComparison.OrdinalIgnoreCase))
                return "kg";

            if (unit.StartsWith(
                    "Gram",
                    StringComparison.OrdinalIgnoreCase))
                return "g";

            if (unit.StartsWith(
                    "Liter",
                    StringComparison.OrdinalIgnoreCase))
                return "L";

            if (unit.StartsWith(
                    "Milliliter",
                    StringComparison.OrdinalIgnoreCase))
                return "mL";

            if (unit.StartsWith(
                    "Meter",
                    StringComparison.OrdinalIgnoreCase))
                return "m";

            if (unit.StartsWith(
                    "Yard",
                    StringComparison.OrdinalIgnoreCase))
                return "yd";

            return unit.ToLowerInvariant();
        }

        // =====================================================
        // CANCEL PRODUCT EDITOR
        // =====================================================

        private void CancelProductEditorButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ProductEditorOverlay.Visibility =
                Visibility.Collapsed;

            _editingProduct = null;
        }

        // =====================================================
        // ARCHIVE PRODUCT
        // =====================================================

        private void ArchiveProductButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "Archiving is not supported by the current Inventory table because it has no archive/status column. The product remains in your inventory.",
                "Archive Unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // =====================================================
        // WEEKLY CHART
        // =====================================================

        private void WeeklyBarCanvas_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            DailyEntryProduct? selected = SelectedProduct;

            if (selected == null)
                return;

            if (!int.TryParse(
                    QuantitySoldInput.Text,
                    out int quantity) ||
                quantity < 0)
            {
                return;
            }

            DateTime selectedWeek = GetWeekStart(
                ReportingDatePicker.SelectedDate ??
                _previewWeekStart);

            int pendingQuantity =
                selectedWeek == _previewWeekStart
                    ? quantity
                    : 0;

            DrawWeeklyProductChart(
                selected,
                pendingQuantity);
        }

        private void DrawWeeklyProductChart(
            DailyEntryProduct selectedProduct,
            int pendingQuantity)
        {
            if (WeeklyBarCanvas == null)
                return;

            WeeklyBarCanvas.Children.Clear();

            double chartWidth =
                WeeklyBarCanvas.ActualWidth;

            if (chartWidth < 180)
                return;

            var chartRows = _products
                .Select(p => new
                {
                    p.Name,

                    Units = p.WeeklyUnits +
                        (p == selectedProduct
                            ? pendingQuantity
                            : 0)
                })
                .Where(p => p.Units > 0)
                .OrderByDescending(p => p.Units)
                .Take(5)
                .ToList();

            if (chartRows.Count == 0)
                return;

            int maximumUnits =
                Math.Max(1, chartRows.Max(p => p.Units));

            double labelWidth =
                Math.Min(135, chartWidth * 0.33);

            double barStart = labelWidth + 10;

            double availableBarWidth = Math.Max(
                45,
                chartWidth - barStart - 38);

            double rowHeight = 42;
            double barHeight = 22;
            double topPadding = 10;

            Line axis = new Line
            {
                X1 = barStart,
                X2 = barStart,
                Y1 = 4,
                Y2 = topPadding +
                     chartRows.Count * rowHeight,
                Stroke = _navyBrush,
                StrokeThickness = 1
            };

            WeeklyBarCanvas.Children.Add(axis);

            Color[] barColors =
            {
                Color.FromRgb(0, 108, 80),
                Color.FromRgb(0, 151, 129),
                Color.FromRgb(0, 169, 145),
                Color.FromRgb(41, 187, 161),
                Color.FromRgb(87, 207, 177)
            };

            for (int i = 0; i < chartRows.Count; i++)
            {
                var item = chartRows[i];

                double y =
                    topPadding + i * rowHeight;

                double barWidth =
                    availableBarWidth *
                    item.Units /
                    maximumUnits;

                AddCanvasLabel(
                    WeeklyBarCanvas,
                    item.Name,
                    0,
                    y + 3,
                    labelWidth - 5,
                    12,
                    false,
                    TextAlignment.Right);

                Rectangle bar = new Rectangle
                {
                    Width = barWidth,
                    Height = barHeight,
                    RadiusX = 3,
                    RadiusY = 3,
                    Fill = new LinearGradientBrush(
                        barColors[i],
                        Color.FromRgb(100, 216, 186),
                        0)
                };

                Canvas.SetLeft(bar, barStart);
                Canvas.SetTop(bar, y);

                WeeklyBarCanvas.Children.Add(bar);

                AddCanvasLabel(
                    WeeklyBarCanvas,
                    item.Units.ToString(),
                    barStart + barWidth + 6,
                    y + 3,
                    34,
                    13,
                    true,
                    TextAlignment.Left);
            }
        }

        private void AddCanvasLabel(
            Canvas canvas,
            string text,
            double x,
            double y,
            double width,
            double fontSize,
            bool isBold,
            TextAlignment alignment)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                Width = width,
                FontSize = fontSize,
                FontWeight = isBold
                    ? FontWeights.Bold
                    : FontWeights.Normal,
                Foreground = _navyBrush,
                TextAlignment = alignment
            };

            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);

            canvas.Children.Add(label);
        }

        // =====================================================
        // FEEDBACK
        // =====================================================

        private void ShowFeedback(
            string message,
            bool isError)
        {
            EntryFeedbackText.Text = message;

            EntryFeedbackText.Foreground =
                new SolidColorBrush(
                    isError
                        ? Color.FromRgb(195, 59, 59)
                        : Color.FromRgb(0, 143, 131));

            EntryFeedbackText.Visibility =
                Visibility.Visible;
        }

    }

    // =========================================================
    // PRODUCT MODEL
    // =========================================================

    public sealed class DailyEntryProduct
    {
        public string DatabaseId { get; set; } = "";
        public Guid ProductId { get; set; }
        public DateTime DateAdded { get; set; }

        public string Name { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string Category { get; set; } = "";

        public string SellingUnit { get; set; } = "";

        public string UnitLabel { get; set; } = "";

        public int WeeklyUnits { get; set; }

        public int CurrentStock { get; set; }

    }

}