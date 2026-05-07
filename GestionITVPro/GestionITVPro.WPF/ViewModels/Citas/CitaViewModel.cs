using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GestionITVPro.Config;
using GestionITVPro.Enums;
using GestionITVPro.Message;
using GestionITVPro.Models;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Dialogs;
using GestionITVPro.WPF.Mapper;
using GestionITVPro.WPF.Views.Cita;
using Serilog;

namespace GestionITVPro.WPF.ViewModels.Citas;

public partial class CitaViewModel : ObservableObject {
    private IDialogService _dialogService;
    private readonly ILogger _logger = Log.ForContext<CitaViewModel>();
    private ICitasService _citasService;
    
   

    [ObservableProperty] private string _motorSeleccionado = "TODOS";

    [ObservableProperty] private ObservableCollection<CitaItemViewModel> _citas = new();

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _mostrarEliminados;

    [ObservableProperty] private TipoOrdenamiento _ordenActual = TipoOrdenamiento.Matricula;

    [ObservableProperty] private int _paginaActual = 1;

    [ObservableProperty] private string _searchText = "";

    [ObservableProperty] private CitaItemViewModel? _selectedCita;

    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private int _tamanoPagina = 10;

    private List<Cita> _todasLasCitas = new();

    [ObservableProperty] private int _totalPaginas;

    [ObservableProperty] private int _totalRegistros;
    
    [ObservableProperty] private DateTime _fechaInicio = DateTime.Today.AddMonths(-1);

    [ObservableProperty] private DateTime? _fechaFin = DateTime.Today.AddMonths(1);

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

    public bool PuedeIrAPaginaAnterior => PaginaActual > 1;
    public bool PuedeIrAPaginaSiguiente => PaginaActual < TotalPaginas;

    partial void OnSearchTextChanged(string value) {
        { PaginaActual = 1; LoadCitas(); }
    }

    partial void OnMotorSeleccionadoChanged(string value) {
        { PaginaActual = 1; LoadCitas(); }
    }

    partial void OnMostrarEliminadosChanged(bool value) {
        FilterCitas();
    }

    partial void OnPaginaActualChanged(int value) {
        LoadCitas();
    }

    partial void OnTamanoPaginaChanged(int value) {
        PaginaActual = 1;
        LoadCitas();
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();
        
    }

    partial void OnSelectedCitaChanged(CitaItemViewModel? value) {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        ViewCommand.NotifyCanExecuteChanged();
    }
    
    
    

    // Al cambiar las fechas, volvemos a filtrar
    partial void OnFechaInicioChanged(DateTime value) => LoadCitas();
    partial void OnFechaFinChanged(DateTime? value) => LoadCitas();

    [RelayCommand(CanExecute = nameof(CanView))]
    private void View() {
        if (SelectedCita == null) return;

        // 1. Creamos la ventana
        var detailsWindow = new CitaDetails();

        // 2. ASIGNACIÓN DIRECTA: Pasamos el modelo directamente al DataContext.
        // Al hacer ToModel(), pasamos el objeto Cita puro. 
        // Ahora los {Binding Marca} funcionarán sin el prefijo "Cita."
        detailsWindow.DataContext = SelectedCita.ToModel();

        // 3. Configuramos el Owner y mostramos
        detailsWindow.Owner = Application.Current.MainWindow;
        detailsWindow.ShowDialog();
    }

    private bool CanView() {
        return SelectedCita != null;
    }

    private void FilterCitas() {
        var filtered = _todasLasCitas.AsEnumerable();
        
        // Filtro de eliminados (si no se muestran, los quitamos)
        if (!MostrarEliminados)
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
            .Select(e => e.ToItemViewModel())
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

    [RelayCommand]
    private void LoadCitas() {
        IsLoading = true;
        StatusMessage = "Cargando datos...";

        try {
            // El servicio decidirá si va a memoria o a SQL según tu configuración
            var result = _citasService.GetByDateMatricula(
                FechaInicio, 
                FechaFin, 
                PaginaActual, 
                TamanoPagina, 
                SearchText,
                MotorSeleccionado,
                isDeleteInclude: MostrarEliminados
            );

            if (result.IsSuccess) {
                // Convertimos los modelos a ViewModels para la lista
                var lista = result.Value.Select(e => e.ToItemViewModel()).ToList();
                Citas = new ObservableCollection<CitaItemViewModel>(lista);
                
                // Actualizamos totales (Paginación)
                ActualizarEstadoPaginacion();
                StatusMessage = $"Registros encontrados: {TotalRegistros}";
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al sincronizar citas");
            StatusMessage = "Error de carga";
        }
        finally {
            IsLoading = false;
        }
    }

    private void ActualizarEstadoPaginacion() {
        // Llamada al nuevo método del servicio que cuenta en SQL
        TotalRegistros = _citasService.CountCitasFiltradas(
            SearchText, FechaInicio, FechaFin, MostrarEliminados);

        TotalPaginas = TotalRegistros == 0 ? 1 : (int)Math.Ceiling((double)TotalRegistros / TamanoPagina);

        // Notificar cambios a comandos de navegación
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();

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
            LoadCitas();
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
            SelectedCita.UpdateFromFormData(editViewModel.FormData);

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

        // Solo llamamos a Restore si el usuario activó el check de "Mostrar Eliminados"
        // y la cita realmente está marcada como borrada.
        if (MostrarEliminados && SelectedCita.IsDeleted) {
            Restore();
            return;
        }
    
        // Mensaje claro que evita confusiones
        var mensaje = "¿Está seguro de que desea eliminar este registro?";

        if (!_dialogService.ShowConfirmation(mensaje, "Confirmar Acción"))
            return;

        var deleteResult = _citasService.Delete(SelectedCita.Id, AppConfig.UseLogicalDelete);
    
        if (deleteResult.IsSuccess) {
            // Recargamos para limpiar el cache y la lista
            LoadCitas(); 
            StatusMessage = "Cita eliminada correctamente";
            WeakReferenceMessenger.Default.Send(new CitaCambiadaMesage());
        } else {
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