using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GestionITVPro.Config;
using GestionITVPro.Enums;
using GestionITVPro.Message;
using GestionITVPro.Models;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Dialogs;
using GestionITVPro.WPF.Views.Cita;
using Serilog;

namespace GestionITVPro.WPF.ViewModels.Citas;

public partial class CitaViewModel : ObservableObject {
    private IDialogService _dialogService;
    private readonly ILogger _logger = Log.ForContext<CitaViewModel>();
    private ICitasService _citasService;


    [ObservableProperty] private string _motorSeleccionado = "Todos";

    [ObservableProperty] private ObservableCollection<CitaItemViewModel> _citas = new();

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _mostrarELiminados;

    [ObservableProperty] private TipoOrdenamiento _ordenActual = TipoOrdenamiento.Matricula;

    [ObservableProperty] private int _paginaActual = 1;

    [ObservableProperty] private string _searchText = "";

    [ObservableProperty] private CitaItemViewModel? _selectedCita;

    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private int _tamanoPagina = 10;

    private List<Cita> _todasLasCitas = new();

    [ObservableProperty] private int _totalPaginas;

    [ObservableProperty] private int _totalRegistros;

    public CitaViewModel(
        ICitasService citasService,
        IDialogService dialogService) {
        _citasService = citasService;
        _dialogService = dialogService;
        LoadCitas();
    }

    public bool UsaBorradoLogico => AppConfig.UseLogicalDelete;

    public IEnumerable<Motor> Motors => Enum.GetValues<Motor>();

    public List<string> MotoresConTodos =>
        new List<string> { "Todos" }.Concat(Motors.Select(c => c.ToString())).ToList();

    public int[] TamanosPagina => [5, 10, 25, 50];

    public bool PuedeIrAnteriorPagina => PaginaActual > 1;
    public bool PuedeIrPaginaSiguiente => PaginaActual < TotalPaginas;

    partial void OnSearchTextChanged(string value) {
        FilterCitas();
    }

    partial void OnMotorSeleccionadoChanged(string value) {
        FilterCitas();
    }

    partial void OnMostrarEliminadosChanged() {
        LoadCitas();
    }

    partial void OnPaginaActualChanged(int value) {
        FilterCitas();
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();
    }

    partial void OnTamañoPaginaChanged(int value) {
        PaginaActual = 1;
        FilterCitas();
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();
        
    }

    partial void OnSelectCitaChanged(CitaItemViewModel? value) {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        ViewCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanView))]
    private void View() {
        if (SelectedCita == null) return;

        var detailsWindow = new CitaDetails() {
            DataContext = new { Cita = SelectedCita.ToModel() },
            Owner = Application.Current.MainWindow
        };
        detailsWindow.ShowDialog();
    }

    private bool CanView() {
        return SelectedCita != null;
    }

    private void FilterCitas() {
        var filtered = _todasLasCitas.AsEnumerable();
        
        // Filtro de eliminados (si no se muestran, los quitamos)
        if (!MostrarELiminados)
            filtered = filtered.Where(e => !e.IsDeleted);
        
        if (MotorSeleccionado != "Todos" && !string.IsNullOrEmpty(MotorSeleccionado))
            if (Enum.TryParse<Motor>(MotorSeleccionado, out var motorEnum))
                filtered = filtered.Where(e => e.Motor == motorEnum);

        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(e =>
                e.Matricula.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                e.Marca.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                e.Modelo.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                e.DniPropietario.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        var listaFiltradaOrdenada = AplicarOrdenamiento(filtered, OrdenActual).ToList();

        TotalRegistros = listaFiltradaOrdenada.Count();
        TotalPaginas = TotalRegistros == 0 ? 1 : (int)Math.Ceiling((double)TotalRegistros / TamanoPagina);

        if (PaginaActual > TotalPaginas)
            _paginaActual = TotalPaginas;

        if (PaginaActual < 1)
            PaginaActual = 1;

        var pagina = listaFiltradaOrdenada
            .Skip((PaginaActual - 1) * TamanoPagina)
            .Take(TamanoPagina)
            .Select(e => e.ToItemVieModel())
            .ToList();

        Citas = new ObservableCollection<CitaItemViewModel>(pagina);

        if (MotorSeleccionado != "Todos" && !string.IsNullOrEmpty(MotorSeleccionado) &&
            !string.IsNullOrWhiteSpace(SearchText))
            StatusMessage =
                $"Página {PaginaActual}/{TotalPaginas} - Mostrando {Citas.Count} de {TotalRegistros} citas";
        else if (MotorSeleccionado != "Todos" && !string.IsNullOrEmpty(MotorSeleccionado))
            StatusMessage =
                $"Página {PaginaActual}/{TotalPaginas} - {Citas.Count} de {TotalRegistros} citas de los {MotorSeleccionado}";
        else if (!string.IsNullOrWhiteSpace(SearchText))
            StatusMessage =
                $"Página {PaginaActual}/{TotalPaginas} - Mostrando {Citas.Count} de {TotalRegistros} citas";
        else
            StatusMessage = $"Página {PaginaActual}/{TotalPaginas} - Total: {TotalRegistros} citas";


    }

    private IEnumerable<Cita> AplicarOrdenamiento(IEnumerable<Cita> lista, TipoOrdenamiento orden) {
        return orden switch {
            TipoOrdenamiento.Matricula => lista.OrderBy(e => e.Matricula),
            TipoOrdenamiento.DniPropietario => lista.OrderBy(e => e.DniPropietario),
            TipoOrdenamiento.FechaItv => lista.OrderBy(e => e.FechaItv),
            TipoOrdenamiento.Marca => lista.OrderBy(e => e.Marca),
            TipoOrdenamiento.Modelo => lista.OrderBy(e => e.Modelo),
            _ => lista.OrderBy(e => e.Id)
        };
    }

    private void LoadCitas() {
        IsLoading = true;
        StatusMessage = "Cargando citas...";

        try {
            var result = _citasService.GetCitasOrderBy(OrdenActual, 1, int.MaxValue, MostrarELiminados);
            _todasLasCitas = result.ToList();
            FilterCitas();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al cargar citas");
            StatusMessage = "Error al cargar";
            _dialogService.ShowError("Error al cargar las citas");
        }
        finally {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void New() {
        var newCita = new Cita {
            Matricula = "",
            Marca = "",
            Modelo = "",
            Cilindrada = 0,
            Motor = Motor.Diesel,
            DniPropietario = "",
            // FechaITV (como matriculación): Hoy es el límite máximo
            FechaItv = DateTime.Today, 
            // FechaInspeccion: Un valor inicial válido (hoy)
            FechaInspeccion = DateTime.Today 
        };

        var editViewModel =
            new CitaEditViewModel(newCita, _citasService, _dialogService, true);
        var editWindow = new CitaEditWindow {
            DataContext = editViewModel,
            Owner = Application.Current.MainWindow
        };

        if (editWindow.ShowDialog() == true) {
            var creado = editViewModel.FormData.ToModel();
            _todasLasCitas.Add(creado);
            FilterCitas();

            StatusMessage = "Cita creada";
            WeakReferenceMessenger.Default.Send(new CitaCambiadaMesage());
        }
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit() {
        if (SelectedCita == null) return;

        var editCita = SelectedCita.ToModel();

        var editViewModel =
            new CitaEditViewModel(editCita, _citasService, _dialogService, false);
        var editWindow = new CitaEditWindow {
            DataContext = editViewModel,
            Owner = Application.Current.MainWindow
        };

        if (editWindow.ShowDialog() == true) {
            SelectedCita.UpdateFromData(editViewModel.FormData);

            var index = _todasLasCitas.FindIndex(e => e.Id == SelectedCita.Id);
            if (index != -1) _todasLasCitas[index] = editViewModel.FormData.ToModel();

            StatusMessage = "Cita actualizada";
            WeakReferenceMessenger.Default.Send(new CitaCambiadaMesage());
        }
    }

    private bool CanEdit() {
        return SelectedCita != null;
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete() {
        if (SelectedCita == null) return;

        if (SelectedCita.IsDeleted) {
            Restore();
            return;
        }
        
        var mensaje = AppConfig.UseLogicalDelete
            ? $"¿Eliminar a {SelectedCita.Descripcion}? El borrado es reversible."
            : $"¿Eliminar a {SelectedCita.Descripcion}? Este borrado es IRREVERSIBLE.";

        if (!_dialogService.ShowConfirmation(mensaje))
            return;

        var deleteResult = _citasService.Delete(SelectedCita.Id, AppConfig.UseLogicalDelete);
        if (deleteResult.IsSuccess) {
            if (AppConfig.UseLogicalDelete) {
                SelectedCita.IsDeleted = true;
                var index = _todasLasCitas.FindIndex(e => e.Id == SelectedCita.Id);
                if (index != -1) _todasLasCitas[index] = _todasLasCitas[index] with { IsDeleted = true };

                if (!MostrarELiminados) Citas.Remove(SelectedCita);
            }
            else {
                _todasLasCitas.RemoveAll(e => e.Id == SelectedCita.Id);
                Citas.Remove(SelectedCita);
            }

            StatusMessage = "Cita eliminada";
            WeakReferenceMessenger.Default.Send(new CitaCambiadaMesage());
        }
        else {
            _dialogService.ShowError(deleteResult.Error.Message);
        }
    }

    private bool CanDelete() {
        return SelectedCita != null;
    }

    [RelayCommand]
    private void Load() {
        SearchText = "";
        MotorSeleccionado = "Todos";
        LoadCitas();
    }

    [RelayCommand]
    private void OrderBy(TipoOrdenamiento orden) {
        OrdenActual = orden;
        FilterCitas();
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Restore() {
        if (SelectedCita == null) return;
        
        if (!_dialogService.ShowConfirmation($"¿Restaurar a {SelectedCita.Descripcion}"))
            return;

        var result = _citasService.Restore(SelectedCita.Id);

        if (result.IsSuccess) {
            SelectedCita.IsDeleted = false;

            var index = _todasLasCitas.FindIndex(e => e.Id == SelectedCita.Id);
            if (index != -1) _todasLasCitas[index] = _todasLasCitas[index] with { IsDeleted = false };


            SelectedCita = null;
            StatusMessage = "Cita restaurada";
            WeakReferenceMessenger.Default.Send(new CitaCambiadaMesage());
        }
        else {
            _dialogService.ShowError($"Error al restaurar: {result.Error.Message}");
        }
    }
    
    
    [RelayCommand(CanExecute = nameof(PuedeIrAPaginaAnterior))]
    private void PaginaAnterior() {
        if (PaginaActual > 1)
            PaginaActual--;
    }

    [RelayCommand(CanExecute = nameof(PuedeIrAPaginaSiguiente))]
    private void PaginaSiguiente() {
        if (PaginaActual < TotalPaginas)
            PaginaActual++;
    }

    [RelayCommand]
    private void PrimeraPagina() {
        PaginaActual = 1;
    }

    [RelayCommand]
    private void UltimaPagina() {
        PaginaActual = TotalPaginas;
    }

    [RelayCommand]
    private void CambiarTamanoPagina(int tamano) {
        TamanoPagina = tamano;
    }
    

} 