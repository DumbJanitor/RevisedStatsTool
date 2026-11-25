using System;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StatsTool.Data;

namespace StatsTool.Views
{
    public partial class SettingsView : UserControl
    {
        // =============================================================
        // FRONTEND TEAM — SETTINGS
        // =============================================================
        //
        // MainWindow owns the sidebar and title bar.
        // Do not rebuild them inside SettingsView.
        //
        // This view handles:
        // - Editing username
        // - Editing email
        // - Entering an optional new password
        // - Validating fields
        // - Displaying action feedback
        // - Confirming a deletion request
        // - Responsive layout
        //
        // The account displayed in XAML is SAMPLE DATA.
        //
        // IMPORTANT:
        // Do not display successful persistence messages until
        // the backend actually confirms the requested changes.
        //
        // =============================================================
        // BACKEND TEAM — REQUIRED INTEGRATION
        // =============================================================
        //
        // 1. ACCOUNT LOADING
        //
        //    Retrieve the currently authenticated user's account.
        //
        //    Populate:
        //    - UsernameTextBox
        //    - EmailTextBox
        //
        //    Never retrieve an account solely from an editable
        //    username supplied by the frontend.
        //
        // 2. ACCOUNT UPDATES
        //
        //    Connect SaveChangesButton_Click to the account service.
        //
        //    Validate:
        //    - Username availability
        //    - Email format and availability
        //    - Optional password requirements
        //
        //    If NewPasswordBox.Password is empty, leave the existing
        //    password unchanged.
        //
        //    Request re-authentication when required by the
        //    application's security policy.
        //
        // 3. ACCOUNT DELETION
        //
        //    Connect DeleteAccountButton_Click to the account service.
        //
        //    Verify the authenticated account and any required
        //    re-authentication before permanently deleting it.
        //
        //    ONLY return to Login after deletion succeeds.
        //
        //    If deletion fails, keep the user signed in and display
        //    the error.
        //
        // 4. SECURITY
        //
        //    Never log passwords or include passwords in error messages.
        //
        // =============================================================
        // DATABASE TEAM — REQUIRED INTEGRATION
        // =============================================================
        //
        // 1. Maintain unique username and email constraints.
        //
        // 2. Store password hashes using an approved password hasher.
        //    Never store plaintext passwords.
        //
        // 3. Enforce account ownership through backend/database
        //    authorization checks.
        //
        // 4. Agree with backend on account deletion behavior:
        //    - Account records
        //    - Products
        //    - Sales entries
        //    - Historical reports
        //
        // 5. Ensure related deletion/update operations preserve
        //    database consistency.
        //
        // =============================================================

        public SettingsView()
        {
            InitializeComponent();

            PasswordHint.Visibility = Visibility.Visible;
            LoadAccount();
        }

        private void LoadAccount()
        {
            try
            {
                if (MainWindow.CurrentUserId == null) return;
                var account = DatabaseService.GetAccount(MainWindow.CurrentUserId);
                UsernameTextBox.Text = account.Name;
                EmailTextBox.Text = account.Email;
            }
            catch (Exception ex)
            {
                ShowStatus("Could not load account: " + ex.Message, true);
            }
        }

        // =============================================================
        // PASSWORD PLACEHOLDER
        // =============================================================

        private void NewPasswordBox_PasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            PasswordHint.Visibility =
                string.IsNullOrEmpty(NewPasswordBox.Password)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        // =============================================================
        // SAVE ACCOUNT CHANGES
        // =============================================================

        private void SaveChangesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string name = UsernameTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowStatus("Please enter a name.", true);
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowStatus("Please enter a valid email address.", true);
                return;
            }

            if (!string.IsNullOrEmpty(NewPasswordBox.Password) &&
                NewPasswordBox.Password.Length < 8)
            {
                ShowStatus("New password must contain at least 8 characters.", true);
                return;
            }

            try
            {
                if (MainWindow.CurrentUserId == null)
                    throw new InvalidOperationException("No signed-in account.");

                DatabaseService.UpdateAccount(
                    MainWindow.CurrentUserId,
                    name,
                    email,
                    string.IsNullOrWhiteSpace(NewPasswordBox.Password)
                        ? null : NewPasswordBox.Password);

                NewPasswordBox.Clear();
                PasswordHint.Visibility = Visibility.Visible;
                ShowStatus("Account changes saved.", false);
            }
            catch (Exception ex)
            {
                ShowStatus("Could not save changes: " + ex.Message, true);
            }
        }

        private void DeleteAccountButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Permanently delete your account? Accounts with inventory products cannot be deleted because those products and their sales history must be preserved.",
                "Confirm Account Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (MainWindow.CurrentUserId == null)
                    throw new InvalidOperationException("No signed-in account.");

                DatabaseService.DeleteAccount(MainWindow.CurrentUserId);
                MainWindow.ClearCurrentUser();
                (Window.GetWindow(this) as MainWindow)?.ShowLogin();
            }
            catch (Exception ex)
            {
                ShowStatus("Could not delete account: " + ex.Message, true);
            }
        }

        // =====================================================
        // EMAIL VALIDATION
        // =============================================================

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                var parsed = new MailAddress(email);

                return string.Equals(
                    parsed.Address,
                    email,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // =============================================================
        // STATUS MESSAGE
        // =============================================================

        private void ShowStatus(
            string message,
            bool isError)
        {
            StatusText.Text = message;

            StatusText.Foreground = new SolidColorBrush(
                isError
                    ? Color.FromRgb(194, 41, 35)
                    : Color.FromRgb(55, 92, 115));

            StatusText.Visibility = Visibility.Visible;
        }

        // =============================================================
        // RESPONSIVE LAYOUT
        // =============================================================
        //
        // Wide window:
        // Labels appear beside their inputs.
        //
        // Narrow window:
        // Labels appear above their inputs.
        //
        // Delete Account moves underneath its explanatory text.
        //
        // No extra controls or separate compact page are necessary.
        // =============================================================

        private void PageRoot_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            if (UsernameRow == null ||
                EmailRow == null ||
                PasswordRow == null ||
                DeleteRow == null)
            {
                return;
            }

            bool compact = e.NewSize.Width < 720;

            ReflowAccountRow(
                UsernameRow,
                UsernameTextBox,
                compact);

            ReflowAccountRow(
                EmailRow,
                EmailTextBox,
                compact);

            ReflowAccountRow(
                PasswordRow,
                PasswordFieldGrid,
                compact);

            // Adjust Delete Account placement.

            Grid.SetRow(
                DeleteAccountButton,
                compact ? 1 : 0);

            Grid.SetColumn(
                DeleteAccountButton,
                compact ? 0 : 1);

            DeleteAccountButton.Margin = compact
                ? new Thickness(0, 14, 0, 0)
                : new Thickness(0);

            DeleteAccountButton.HorizontalAlignment = compact
                ? HorizontalAlignment.Left
                : HorizontalAlignment.Right;

            DeleteInfo.Margin = compact
                ? new Thickness(0)
                : new Thickness(0, 0, 18, 0);
        }

        // =============================================================
        // RESPONSIVE FIELD ARRANGEMENT
        // =============================================================

        private static void ReflowAccountRow(
            Grid row,
            FrameworkElement field,
            bool compact)
        {
            row.ColumnDefinitions[0].Width = compact
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(220);

            row.ColumnDefinitions[1].Width = compact
                ? new GridLength(0)
                : new GridLength(1, GridUnitType.Star);

            Grid.SetRow(
                field,
                compact ? 1 : 0);

            Grid.SetColumn(
                field,
                compact ? 0 : 1);

            field.Margin = compact
                ? new Thickness(0, 8, 0, 0)
                : new Thickness(0);
        }
    }
}