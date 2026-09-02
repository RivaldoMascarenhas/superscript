using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class SoftwareCatalogViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly ISoftwareService _softwareService;
    private readonly ICatalogSyncService _catalogSyncService;
    private readonly ILogService _logger;

    private string _searchText = string.Empty;
    private string _selectedCategory = "Todos";
    private string _selectedSource = "Todos";
    private bool _isBusy;
    private string _operationStatus = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterSoftware();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                FilterSoftware();
            }
        }
    }

    public string SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                FilterSoftware();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string OperationStatus
    {
        get => _operationStatus;
        set => SetProperty(ref _operationStatus, value);
    }

    public int SelectedCount => AllItems.Count(s => s.IsSelected);

    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<string> Sources { get; } = new() { "Todos", "UniFAP", "WinUtil" };
    public ObservableCollection<SoftwareItem> AllItems { get; } = new();
    public ObservableCollection<SoftwareItem> FilteredItems { get; } = new();

    public ICommand SelectCategoryCommand { get; }
    public ICommand SelectSourceCommand { get; }
    public ICommand InstallSingleSoftwareCommand { get; }
    public ICommand UninstallSingleSoftwareCommand { get; }
    public ICommand RepairSingleSoftwareCommand { get; }
    public ICommand OpenOfficialLinkCommand { get; }
    public ICommand SyncCatalogCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand InstallSelectedCommand { get; }

    public SoftwareCatalogViewModel(
        IConfigService configService,
        ISoftwareService softwareService,
        ICatalogSyncService catalogSyncService,
        ILogService logger)
    {
        _configService = configService;
        _softwareService = softwareService;
        _catalogSyncService = catalogSyncService;
        _logger = logger;

        SelectCategoryCommand = new RelayCommand(param =>
        {
            if (param is string cat) SelectedCategory = cat;
        });

        SelectSourceCommand = new RelayCommand(param =>
        {
            if (param is string src) SelectedSource = src;
        });

        InstallSingleSoftwareCommand = new AsyncRelayCommand(InstallSingleAsync);
        UninstallSingleSoftwareCommand = new AsyncRelayCommand(UninstallSingleAsync);
        RepairSingleSoftwareCommand = new AsyncRelayCommand(RepairSingleAsync);
        OpenOfficialLinkCommand = new RelayCommand(OpenOfficialLink);
        SyncCatalogCommand = new AsyncRelayCommand(SyncCatalogAsync);
        SelectAllCommand = new RelayCommand(SelectAll);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        InstallSelectedCommand = new AsyncRelayCommand(InstallSelectedAsync);
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        OperationStatus = "Carregando catálogo unificado...";

        try
        {
            var items = await _catalogSyncService.GetMergedCatalogAsync();

            Categories.Clear();
            Categories.Add("Todos");
            var distinctCats = items.Select(i => i.Category).Distinct().OrderBy(c => c);
            foreach (var cat in distinctCats)
            {
                Categories.Add(cat);
            }

            AllItems.Clear();
            foreach (var item in items)
            {
                AllItems.Add(item);
            }

            FilterSoftware();
            UpdateSelectedCount();
            OperationStatus = $"Catálogo carregado: {AllItems.Count} softwares disponíveis.";
        }
        catch (Exception ex)
        {
            _logger.LogError("SoftwareCatalogViewModel", "Erro ao inicializar catálogo", ex);
            OperationStatus = $"Erro ao carregar catálogo: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void FilterSoftware()
    {
        FilteredItems.Clear();
        var items = AllItems.AsEnumerable();

        if (SelectedCategory != "Todos")
        {
            items = items.Where(s => s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedSource != "Todos")
        {
            if (SelectedSource == "UniFAP")
            {
                items = items.Where(s => s.Source.Contains("UniFAP", StringComparison.OrdinalIgnoreCase));
            }
            else if (SelectedSource == "WinUtil")
            {
                items = items.Where(s => s.Source.Contains("WinUtil", StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            items = items.Where(s => s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     s.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     s.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
        {
            FilteredItems.Add(item);
        }
    }

    public void UpdateSelectedCount()
    {
        OnPropertyChanged(nameof(SelectedCount));
    }

    private void SelectAll()
    {
        foreach (var item in FilteredItems)
        {
            item.IsSelected = true;
        }
        UpdateSelectedCount();
    }

    private void ClearSelection()
    {
        foreach (var item in AllItems)
        {
            item.IsSelected = false;
        }
        UpdateSelectedCount();
    }

    private async Task InstallSelectedAsync(object? param)
    {
        var selectedList = AllItems.Where(s => s.IsSelected).ToList();
        if (selectedList.Count == 0) return;

        IsBusy = true;
        int total = selectedList.Count;
        int successCount = 0;
        int failureCount = 0;

        _logger.LogInformation("SoftwareCatalogViewModel", $"Iniciando instalação em lote de {total} softwares selecionados...");

        for (int i = 0; i < total; i++)
        {
            var sw = selectedList[i];
            OperationStatus = $"[{i + 1}/{total}] Instalando {sw.Name}...";
            sw.Status = SoftwareInstallStatus.Installing;

            try
            {
                var res = await _softwareService.InstallAsync(sw, dryRun: false, msg => OperationStatus = msg);
                sw.Status = res.Status;
                sw.ErrorMessage = res.Message;

                if (res.Success) successCount++;
                else failureCount++;
            }
            catch (Exception ex)
            {
                sw.Status = SoftwareInstallStatus.Failed;
                sw.ErrorMessage = ex.Message;
                failureCount++;
            }
        }

        IsBusy = false;
        OperationStatus = failureCount == 0
            ? $"Todos os {successCount} softwares foram instalados com sucesso!"
            : $"Instalação concluída: {successCount} instalados, {failureCount} avisos/falhas.";
    }

    private async Task InstallSingleAsync(object? param)
    {
        if (param is not SoftwareItem sw) return;

        IsBusy = true;
        OperationStatus = $"Instalando {sw.Name}...";
        sw.Status = SoftwareInstallStatus.Installing;

        try
        {
            var res = await _softwareService.InstallAsync(sw, dryRun: false, msg => OperationStatus = msg);
            sw.Status = res.Status;
            sw.ErrorMessage = res.Message;
            OperationStatus = res.Success ? $"{sw.Name} instalado com sucesso!" : $"Falha ao instalar {sw.Name}.";
        }
        catch (Exception ex)
        {
            sw.Status = SoftwareInstallStatus.Failed;
            sw.ErrorMessage = ex.Message;
            OperationStatus = $"Erro: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task UninstallSingleAsync(object? param)
    {
        if (param is not SoftwareItem sw) return;

        IsBusy = true;
        OperationStatus = $"Removendo {sw.Name}...";
        try
        {
            bool uninstalled = await _softwareService.UninstallAsync(sw);
            OperationStatus = uninstalled ? $"{sw.Name} desinstalado com sucesso." : $"Não foi possível desinstalar {sw.Name}.";
            sw.Status = SoftwareInstallStatus.Pending;
        }
        catch (Exception ex)
        {
            OperationStatus = $"Erro ao desinstalar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RepairSingleAsync(object? param)
    {
        if (param is not SoftwareItem sw) return;

        IsBusy = true;
        OperationStatus = $"Reparando {sw.Name}...";
        try
        {
            bool repaired = await _softwareService.RepairAsync(sw);
            OperationStatus = repaired ? $"{sw.Name} reparado com sucesso." : $"Não foi possível reparar {sw.Name}.";
            sw.Status = repaired ? SoftwareInstallStatus.Installed : SoftwareInstallStatus.Warning;
        }
        catch (Exception ex)
        {
            OperationStatus = $"Erro no reparo: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenOfficialLink(object? param)
    {
        if (param is not string url || string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SoftwareCatalogViewModel", $"Erro ao abrir link {url}: {ex.Message}");
        }
    }

    private async Task SyncCatalogAsync(object? param)
    {
        IsBusy = true;
        OperationStatus = "Sincronizando catálogo oficial do WinUtil...";

        try
        {
            var res = await _catalogSyncService.SyncWinUtilCatalogAsync(forceOnline: true);
            OperationStatus = res.Message;
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            OperationStatus = $"Erro na sincronização: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
