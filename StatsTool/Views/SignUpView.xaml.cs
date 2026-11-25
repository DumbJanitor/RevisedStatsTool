using System;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StatsTool.Data;

namespace StatsTool.Views
{
    public partial class SignUpView : UserControl
    {
        private bool _passwordVisible;

        public SignUpView()
        {
            InitializeComponent();
        }

        // =====================================================
        // MAIN WINDOW REFERENCE
        // =====================================================

        private StatsTool.MainWindow? GetMainWindow()
        {
            return Window.GetWindow(this) as StatsTool.MainWindow;
        }

        // =====================================================
        // BACK TO LOGIN
        // =====================================================

        private void BackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            GetMainWindow()?.ShowLogin();
        }

        // =====================================================
        // PASSWORD VISIBILITY — FRONTEND TEAM
        // =====================================================
        // Only the password may be revealed; username and email
        // stay as normal, fully readable TextBoxes at all times.
        // Hidden mode clears the plaintext display TextBox.
        // BACKEND TEAM: PasswordInput remains the registration source.
        // DATABASE TEAM: This event must never save a password.

        private void PasswordVisibilityButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_passwordVisible)
            {
                VisiblePasswordTextBox.Text = PasswordInput.Password;
                PasswordInput.Visibility = Visibility.Collapsed;
                VisiblePasswordTextBox.Visibility = Visibility.Visible;
                PasswordVisibilityButton.Content = "Hide";
                _passwordVisible = true;
                VisiblePasswordTextBox.Focus();
                VisiblePasswordTextBox.CaretIndex =
                    VisiblePasswordTextBox.Text.Length;
            }
            else
            {
                PasswordInput.Password = VisiblePasswordTextBox.Text;
                VisiblePasswordTextBox.Clear();
                VisiblePasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                PasswordVisibilityButton.Content = "Show";
                _passwordVisible = false;
                PasswordInput.Focus();
            }
        }

        // =====================================================
        // SIGN UP
        // =====================================================

        private void SignUpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            FeedbackText.Visibility = Visibility.Collapsed;
            string name = UsernameTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();

            // Synchronize when the user typed in revealed mode.
            // Existing backend integration continues to read PasswordInput.
            if (_passwordVisible)
                PasswordInput.Password = VisiblePasswordTextBox.Text;

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(PasswordInput.Password))
            {
            // -----------------------------------------------
            // FRONTEND VALIDATION
            // -----------------------------------------------
                ShowFeedback("Please complete all fields.", true);
                return;
            }

            // Basic email format check.
            // Backend must perform authoritative validation.
            bool emailIsValid =
                MailAddress.TryCreate(email, out MailAddress? parsedEmail) &&
                string.Equals(parsedEmail?.Address, email, StringComparison.OrdinalIgnoreCase);

            if (!emailIsValid)
            {
                ShowFeedback("Please enter a valid email address.", true);
                return;
            }

            // =================================================
            // DATABASE AUTHENTICATION - ACCOUNT REGISTRATION
            // =================================================
            //
            // 1. Connect to the SQL Server database.
            // 2. Generate a unique user ID.
            // 3. Insert the username, email, and password.
            // 4. Assign the default Client role.
            // 5. Return to Login after successful registration.
            //
            // SECURITY:
            // - Do not hardcode usernames or passwords.
            // - Use SQL parameters to prevent SQL injection.
            // - Password hashing should be implemented before
            //   using this registration system in production.
            //
            // DATABASE TEAM:
            // - U_ID is generated automatically.
            // - U_Email must be unique.
            // - U_Name is stored in the U_Name column.
            //
            // =================================================
            if (PasswordInput.Password.Length < 8)
            {
                ShowFeedback("Password must contain at least 8 characters.", true);
                return;
            }

            try
            {
                    // Generate the next user ID.
                    // Format: U0001, U0002, U0003, and so on.
                            // REGISTRATION SUCCESSFUL:
                            // The new account has been saved to SQL Server.
                            // Return to Login after successful registration.
                            // REGISTRATION FAILED:
                            // No account was inserted.
                DatabaseService.Register(name, email, PasswordInput.Password);
                MessageBox.Show("Account created. You can now sign in.",
                    "Registration Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                GetMainWindow()?.ShowLogin();
            }
            catch (Exception ex)
            {
                // DUPLICATE EMAIL:
                // The email already exists in the User table.
                // DATABASE ERROR:
                // Display an error if the database connection fails.
                ShowFeedback(ex.Message, true);
            }
        }

        // =====================================================
        // GOOGLE SIGN UP
        // =====================================================

        private void GoogleSignUpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // =================================================
            // BACKEND TEAM - GOOGLE AUTHENTICATION
            // =================================================
            //
            // Implement Google OAuth / OpenID Connect.
            //
            // Expected behavior:
            //
            // 1. Begin authentication using the system browser.
            // 2. Complete Google's secure authorization flow.
            // 3. Validate the response through backend.
            // 4. Create or retrieve the corresponding user.
            // 5. Establish an authenticated session.
            // 6. Navigate to Dashboard on success.
            //
            // Do not collect Google passwords in this app.
            // Do not embed client secrets in frontend code.
            //
            // DATABASE TEAM:
            // Coordinate storage of the external provider's
            // account identifier with backend.
            //
            // =================================================
            // TEMPORARY FRONTEND BEHAVIOR
            // =================================================

            ShowFeedback(
                "Google sign-up will be available " +
                "after authentication is connected.",
                false);
        }

        // =====================================================
        // INLINE FEEDBACK
        // =====================================================

        private void ShowFeedback(string message, bool isError)
        {
            FeedbackText.Text = message;

            FeedbackText.Foreground = new SolidColorBrush(
                isError
                    ? Color.FromRgb(195, 59, 59)
                    : Color.FromRgb(0, 143, 131));

            FeedbackText.Visibility = Visibility.Visible;
        }
    }
}
