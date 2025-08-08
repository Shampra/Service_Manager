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
        private int _serviceKeyToEdit = 0;

        public WindowConfig()
        {
            InitializeComponent();
        }

        public WindowConfig(myService service, int serviceKey)
        {
            InitializeComponent();
            _serviceKeyToEdit = serviceKey;

            textBoxName.Text = service.serviceId;
            textBoxTitle.Text = service.serviceName;
            textBoxIP.Text = service.serviceIP;
            textBoxTooltip.Text = service.serviceTooltip;
            textBoxCategory.Text = service.serviceCat;
            textBoxLogPath.Text = service.serviceLog;
            textBoxConfigPath.Text = service.serviceConf;

            this.Title = "Modifier un service";
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(textBoxName.Text) || string.IsNullOrWhiteSpace(textBoxIP.Text))
            {
                MessageBox.Show("Le nom du service et l'IP/nom dus erveur sont requis.", "Erreur de validation", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Open the configuration file
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                AppSettingsSection appSettings = config.AppSettings;

                if (_serviceKeyToEdit == 0) // Add new service
                {
                    // Find the next available service ID
                    int nextServiceId = 1;
                    while (appSettings.Settings["Service" + nextServiceId] != null)
                    {
                        nextServiceId++;
                    }
                    _serviceKeyToEdit = nextServiceId;
                }

                // Add or update the service
                string title = string.IsNullOrWhiteSpace(textBoxTitle.Text) ? textBoxName.Text : textBoxTitle.Text;
                string serviceValue = string.Format("{0};{1};{2}", textBoxName.Text, title, textBoxIP.Text);
                if (appSettings.Settings["Service" + _serviceKeyToEdit] != null)
                    appSettings.Settings["Service" + _serviceKeyToEdit].Value = serviceValue;
                else
                    appSettings.Settings.Add("Service" + _serviceKeyToEdit, serviceValue);

                // Add or update optional settings
                UpdateOptionalSetting(appSettings, "Tooltip_" + _serviceKeyToEdit, textBoxTooltip.Text);
                UpdateOptionalSetting(appSettings, "Cat_" + _serviceKeyToEdit, textBoxCategory.Text);
                UpdateOptionalSetting(appSettings, "Log_" + _serviceKeyToEdit, textBoxLogPath.Text);
                UpdateOptionalSetting(appSettings, "Conf_" + _serviceKeyToEdit, textBoxConfigPath.Text);

                // Save the changes
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                this.Close();
            }
            catch (ConfigurationErrorsException ex)
            {
                MessageBox.Show("Error d'écriture du fichier de configuration: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateOptionalSetting(AppSettingsSection appSettings, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (appSettings.Settings[key] != null)
                {
                    appSettings.Settings[key].Value = value;
                }
                else
                {
                    appSettings.Settings.Add(key, value);
                }
            }
            else
            {
                if (appSettings.Settings[key] != null)
                {
                    appSettings.Settings.Remove(key);
                }
            }
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
