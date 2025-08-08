using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Configuration;

namespace Service_Manager
{
    /// <summary>
    /// Logique d'interaction pour WindowConfig.xaml
    /// </summary>
    public partial class WindowConfig : Window
    {
        public WindowConfig()
        {
            InitializeComponent();
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(textBoxName.Text) || string.IsNullOrWhiteSpace(textBoxIP.Text))
            {
                MessageBox.Show("Service Name and IP/Server are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Open the configuration file
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                AppSettingsSection appSettings = config.AppSettings;

                // Find the next available service ID
                int nextServiceId = 1;
                while (appSettings.Settings["Service" + nextServiceId] != null)
                {
                    nextServiceId++;
                }

                // Add the new service
                string title = string.IsNullOrWhiteSpace(textBoxTitle.Text) ? textBoxName.Text : textBoxTitle.Text;
                string serviceValue = $"{textBoxName.Text};{title};{textBoxIP.Text}";
                appSettings.Settings.Add("Service" + nextServiceId, serviceValue);

                // Add optional settings
                if (!string.IsNullOrWhiteSpace(textBoxTooltip.Text))
                {
                    appSettings.Settings.Add("Tooltip_" + nextServiceId, textBoxTooltip.Text);
                }
                if (!string.IsNullOrWhiteSpace(textBoxCategory.Text))
                {
                    appSettings.Settings.Add("Cat_" + nextServiceId, textBoxCategory.Text);
                }
                if (!string.IsNullOrWhiteSpace(textBoxLogPath.Text))
                {
                    appSettings.Settings.Add("Log_" + nextServiceId, textBoxLogPath.Text);
                }
                if (!string.IsNullOrWhiteSpace(textBoxConfigPath.Text))
                {
                    appSettings.Settings.Add("Conf_" + nextServiceId, textBoxConfigPath.Text);
                }

                // Save the changes
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                this.Close();
            }
            catch (ConfigurationErrorsException ex)
            {
                MessageBox.Show("Error writing to configuration file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
