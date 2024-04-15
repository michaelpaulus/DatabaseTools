using System;
using System.Collections.ObjectModel;
using System.Linq;
using Temelie.Database.Configuration.Preferences;
using Temelie.Database.Views;
using Temelie.DependencyInjection;
using PropertyChanged;

namespace Temelie.Database.ViewModels;
[ExportTransient(typeof(ConnectionViewModel))]
public class ConnectionViewModel : ViewModel
{

    public ConnectionViewModel()
    {
        this.EditCommand = new Command(this.Edit);

        this.LoadConnections();
    }

    public Command EditCommand { get; set; }

    public Models.ConnectionStringModel SelectedConnection { get; set; }

    private ObservableCollection<Models.ConnectionStringModel> _connnections;
    public ObservableCollection<Models.ConnectionStringModel> Connections
    {
        get
        {
            if (_connnections == null)
            {
                _connnections = new ObservableCollection<Models.ConnectionStringModel>();
            }
            return _connnections;
        }
    }

    public bool IsSource { get; set; }

    public void LoadConnections()
    {
        this.Connections.Clear();
        foreach (var item in UserSettingsContext.Current.Connections.OrderBy(i => i.Name))
        {
            this.Connections.Add(item);
        }

        if (this.IsSource)
        {
            var current = (from i in this.Connections where i.Name == UserSettingsContext.Current.SourceConnectionString select i).FirstOrDefault();
            if (current != null)
            {
                this.SelectedConnection = current;
            }
        }
        else
        {
            var current = (from i in this.Connections where i.Name == UserSettingsContext.Current.TargetConnectionString select i).FirstOrDefault();
            if (current != null)
            {
                this.SelectedConnection = current;
            }
        }

        this.OnSelectionChanged(EventArgs.Empty);

    }

    private void Edit()
    {
        var dialog = new DatabaseConnectionDialog();
        dialog.ShowDialog();
        this.LoadConnections();
    }

    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(SelectedConnection):
                if (SelectedConnection != null)
                {
                    if (this.IsSource)
                    {
                        UserSettingsContext.Current.SourceConnectionString = SelectedConnection.Name;
                    }
                    else
                    {
                        UserSettingsContext.Current.TargetConnectionString = SelectedConnection.Name;
                    }
                    UserSettingsContext.Save();
                }
                this.OnSelectionChanged(EventArgs.Empty);
                break;
            case nameof(IsSource):
                this.LoadConnections();
                break;
        }
    }

    #region Event Raising Methods

    [SuppressPropertyChangedWarnings]
    protected virtual void OnSelectionChanged(EventArgs e)
    {
        if (SelectionChanged != null)
        {
            SelectionChanged(this, e);
        }
    }

    #endregion

    #region Events

    public event EventHandler SelectionChanged;

    #endregion

}
