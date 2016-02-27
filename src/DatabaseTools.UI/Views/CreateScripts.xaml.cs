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

namespace DatabaseTools
{
    public partial class CreateScripts
    {
        public CreateScripts()
        {
            this.InitializeComponent();
            SubscribeToEvents();
            this.DataContext = this.ViewModel;
            this.DatabaseConnection.ViewModel.LoadConnections();

        }

        private ViewModels.CreateScriptsViewModel _viewModel;
        public ViewModels.CreateScriptsViewModel ViewModel
        {
            get
            {
                if (this._viewModel == null)
                {
                    this._viewModel = new ViewModels.CreateScriptsViewModel();
                }
                return this._viewModel;
            }
        }

        #region Event Handlers

        private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            using (System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog())
            {

                if (!(string.IsNullOrEmpty(this.ViewModel.ScriptPath)) && System.IO.Directory.Exists(this.ViewModel.ScriptPath))
                {
                    fd.SelectedPath = this.ViewModel.ScriptPath;
                }

                if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    this.ViewModel.ScriptPath = fd.SelectedPath;
                }
            }
        }

        private void DatabaseConnection_SelectionChanged(object sender, EventArgs e)
        {
            this.ViewModel.DatabaseConnectionString = this.DatabaseConnection.ConnectionString;
        }

        #endregion

        private bool EventsSubscribed = false;
        private void SubscribeToEvents()
        {
            if (EventsSubscribed)
                return;
            else
                EventsSubscribed = true;

            BrowseButton.Click += BrowseButton_Click;

            DatabaseConnection.ViewModel.SelectionChanged += DatabaseConnection_SelectionChanged;

        }

        
    }

}