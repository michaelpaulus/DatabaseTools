using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Temelie.Database.Models;
using Temelie.Database.Providers;
using Temelie.Database.Services;

namespace Temelie.Database.Controls;

/// <summary>
/// Interaction logic for Connection.xaml
/// </summary>
public partial class DatabaseConnection : UserControl
{

    public DatabaseConnection()
    {
        InitializeComponent();
        this.DataContext = new ViewModels.ConnectionViewModel();
    }

    public ViewModels.ConnectionViewModel ViewModel
    {
        get
        {
            return (ViewModels.ConnectionViewModel)DataContext;
        }
    }

    public bool IsSource
    {
        get
        {
            return this.ViewModel.IsSource;
        }
        set
        {
            this.ViewModel.IsSource = value;
        }
    }

    public ConnectionStringModel ConnectionString
    {
        get
        {
            return this.ViewModel.SelectedConnection;
        }
    }

    public static IList<Temelie.Database.Models.TableModel> GetTables(IDatabaseExecutionService databaseExecutionService, IDatabaseFactory databaseFactory, ConnectionStringModel connectionString)
    {
        IList<Temelie.Database.Models.TableModel> tables = new List<Temelie.Database.Models.TableModel>();

        try
        {
            using (var conn = databaseExecutionService.CreateDbConnection(connectionString))
            {
                var provider = databaseFactory.GetDatabaseProvider(conn);
                var columns = provider.GetTableColumns(conn);
                tables = provider.GetTables(conn, columns).ToList();
            }
        }
        catch
        {

        }

        return tables;
    }

    public static IList<Temelie.Database.Models.TableModel> GetViews(IDatabaseExecutionService databaseExecutionService, IDatabaseFactory databaseFactory, ConnectionStringModel connectionString)
    {
        IList<Temelie.Database.Models.TableModel> tables = new List<Temelie.Database.Models.TableModel>();

        try
        {
            using (var conn = databaseExecutionService.CreateDbConnection(connectionString))
            {
                var provider = databaseFactory.GetDatabaseProvider(conn);
                var columns = provider.GetTableColumns(conn);
                tables = provider.GetTables(conn, columns).ToList();
            }
        }
        catch
        {

        }

        return tables;
    }

}
