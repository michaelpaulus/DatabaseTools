﻿
using System.Linq;
using System.Xml.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Win32;
using System.IO;
using DatabaseTools.Processes;
using DatabaseTools.Providers;
using DatabaseTools.UI;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseTools
{
    public partial class CreateInsertScript
    {

        private readonly IEnumerable<IDatabaseProvider> _databaseProviders;
        private readonly IEnumerable<IConnectionCreatedNotification> _connectionCreatedNotifications;

        public CreateInsertScript()
        {
            _databaseProviders = ((IServiceProviderApplication)Application.Current).ServiceProvider.GetServices<IDatabaseProvider>();
            _connectionCreatedNotifications = ((IServiceProviderApplication)Application.Current).ServiceProvider.GetServices<IConnectionCreatedNotification>();
            this.InitializeComponent();
            SubscribeToEvents();

            this.DatabaseConnection.ViewModel.LoadConnections();
        }

        private void DatabaseConnection_SelectionChanged(object sender, EventArgs e)
        {
            this.TableComboBox.ItemsSource = null;
            Action<object> action = new Action<object>((object obj) =>
            {
                try
                {
                    var connectionString = (System.Configuration.ConnectionStringSettings)obj;
                    var tables = Controls.DatabaseConnection.GetTables(connectionString, _databaseProviders, _connectionCreatedNotifications);
                    var views = Controls.DatabaseConnection.GetViews(connectionString, _databaseProviders, _connectionCreatedNotifications);

                    var list = tables.Union(views).ToList();

                    Dispatcher.Invoke(new Action<IEnumerable>((IEnumerable results) =>
                    {
                        this.TableComboBox.ItemsSource = results;
                    }), list);
                }
                catch
                {

                }
            });

            System.Threading.Tasks.Task.Factory.StartNew(action, this.DatabaseConnection.ConnectionString);
        }

        private void GenerateScriptButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DatabaseTools.Models.DatabaseModel database = new DatabaseTools.Models.DatabaseModel(this.DatabaseConnection.ConnectionString, _databaseProviders, _connectionCreatedNotifications);
            this.ResultTextBox.Text = database.GetInsertScript(this.TableComboBox.Text, WhereTextBox.Text);
        }

        private void SaveToFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "Sql Files (*.sql)|*.sql|All Files (*.*)|*.*";
            dialog.DefaultExt = "sql";
            if (dialog.ShowDialog().GetValueOrDefault())
            {
                File.WriteAllText(dialog.FileName, ResultTextBox.Text, System.Text.Encoding.UTF8);
            }
        }


        private bool EventsSubscribed = false;
        private void SubscribeToEvents()
        {
            if (EventsSubscribed)
                return;
            else
                EventsSubscribed = true;

            DatabaseConnection.ViewModel.SelectionChanged += DatabaseConnection_SelectionChanged;
            GenerateScriptButton.Click += GenerateScriptButton_Click;
            SaveToFile.Click += SaveToFile_Click;
        }

    }

}