
using System;
using System.Collections.Generic;
using System.Windows;
using Cornerstone.Database.Providers;
using Cornerstone.Database.Services;
using Cornerstone.Database.UI;
using Microsoft.Extensions.DependencyInjection;

namespace Cornerstone.Database;

public partial class Convert
{

    private readonly IEnumerable<IDatabaseProvider> _databaseProviders;
    private readonly IEnumerable<IConnectionCreatedNotification> _connectionCreatedNotifications;

    public Convert()
    {
        _databaseProviders = ((IServiceProviderApplication)Application.Current).ServiceProvider.GetServices<IDatabaseProvider>();
        _connectionCreatedNotifications = ((IServiceProviderApplication)Application.Current).ServiceProvider.GetServices<IConnectionCreatedNotification>();

        // This call is required by the Windows Form Designer.
        InitializeComponent();

        // Add any initialization after the InitializeComponent() call.
        this.SourceDatabaseConnection.IsSource = true;
        SubscribeToEvents();

        this.DataContext = this.ViewModel;

        this.SourceDatabaseConnection.ViewModel.LoadConnections();
        this.TargetDatabaseConnection.ViewModel.LoadConnections();

    }

    private ViewModels.ConvertViewModel _viewModel;
    public ViewModels.ConvertViewModel ViewModel
    {
        get
        {
            if (this._viewModel == null)
            {
                this._viewModel = new ViewModels.ConvertViewModel(_databaseProviders, _connectionCreatedNotifications);
            }
            return this._viewModel;
        }
    }

    #region Methods

    private void UpdateColumns()
    {
        this.ResultsGridView.Columns[2].Width = this.ResultsListBox.ActualWidth - this.ResultsGridView.Columns[0].ActualWidth - this.ResultsGridView.Columns[1].ActualWidth - 30;
    }

    #endregion

    #region Event Handlers

    private void SourceDatabaseConnection_SelectionChanged(object sender, EventArgs e)
    {
        this.ViewModel.SourceDatabaseConnectionString = this.SourceDatabaseConnection.ConnectionString;
    }

    private void TargetDatabaseConnection_SelectionChanged(object sender, EventArgs e)
    {
        this.ViewModel.TargetDatabaseConnectionString = this.TargetDatabaseConnection.ConnectionString;
    }

    private void ResultsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        this.UpdateColumns();
    }

    private void ResultsListBox_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        this.UpdateColumns();
    }

    private void ResultsListBox_SourceUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
    {
        this.UpdateColumns();
    }

    #endregion

    private bool EventsSubscribed;
    private void SubscribeToEvents()
    {
        if (EventsSubscribed)
        {
            return;
        }
        else
        {
            EventsSubscribed = true;
        }

        ResultsListBox.SelectionChanged += ResultsListBox_SelectionChanged;
        ResultsListBox.SizeChanged += ResultsListBox_SizeChanged;
        ResultsListBox.SourceUpdated += ResultsListBox_SourceUpdated;
        SourceDatabaseConnection.ViewModel.SelectionChanged += SourceDatabaseConnection_SelectionChanged;
        TargetDatabaseConnection.ViewModel.SelectionChanged += TargetDatabaseConnection_SelectionChanged;

    }

}
