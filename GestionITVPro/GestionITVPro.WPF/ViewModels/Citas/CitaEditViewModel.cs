using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Enums;
using GestionITVPro.Mapper;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Dialogs;
using GestionITVPro.Validator;
using GestionITVPro.WPF.ViewModels.Form;
using GestionITVPro.Models;
using GestionITVPro.WPF.Mapper; // Asegúrate de tener tus extensiones ToModel() aquí
using Serilog;

namespace GestionITVPro.WPF.ViewModels.Citas;

public partial class CitaEditViewModel(
      Cita cita,
     ICitasService citasService,
     IDialogService dialogService,
     bool isNew
) : ObservableObject {
     private readonly IDialogService _dialogService = dialogService;
     private readonly bool _isNew = isNew;
     private readonly ILogger _logger = Log.ForContext<CitaEditViewModel>();
     private readonly ICitasService _citasService = citasService;

     [ObservableProperty] private CitaFormData _formData = cita.ToFormData();

     [ObservableProperty] private string _windowTitle = isNew ? "Nueva Cita" : "Editar Cita";

     public IEnumerable<Motor> Motors => Enum.GetValues<Motor>();
     
     public Action<bool>? CloseAction { get; set; }

     [RelayCommand]
     private void Save() {
          // 1. Validación de IDataErrorInfo (Campos obligatorios, formatos, etc.)
          if (!FormData.IsValid()) {
               _dialogService.ShowWarning(
                    $"Se han detectado los siguientes errores de validación:\n\n{FormData.GetValidationErrors()}",
                    "Errores de validación");
               return;
          }

          // 2. Validaciones de lógica de negocio (Fechas)
          if (!FormData.FechaInspeccion.IsWithinNext30Days()) {
               _dialogService.ShowWarning(
                    "La fecha de inspección debe estar entre la fecha actual y 30 días como máximo.",
                    "Errores de validación");
               return;
          }

          if (!FormData.FechaItv.IsValidFechaCita()) {
               _dialogService.ShowWarning(
                    "La fecha de matriculación no puede ser futura.",
                    "Errores de validación");
               return; // Faltaba el return en tu código original
          }

          try {
               // 3. Mapeo de FormData a Modelo de Dominio
               var modelo = FormData.ToModel();

               // 4. Preservar metadatos si es una edición
               if (!_isNew) {
                    modelo = modelo with {
                        Id = cita.Id, // Aseguramos mantener el ID original
                        CreatedAt = cita.CreatedAt,
                        IsDeleted = cita.IsDeleted,
                        DeletedAt = cita.DeletedAt
                    };
               }

               // 5. Persistencia a través del Servicio
               var result = _isNew
                    ? _citasService.Save(modelo)
                    : _citasService.Update(modelo.Id, modelo);

               // 6. Manejo del resultado (Result Pattern)
               if (result.IsSuccess) {
                    _logger.Information("Cita para matrícula {Matricula} guardada correctamente", modelo.Matricula);
                    CloseAction?.Invoke(true);
               }
               else {
                    _logger.Warning("Error de negocio al guardar: {Error}", result.Error.Message);
                    _dialogService.ShowError(result.Error.Message);
               }
          }
          catch (Exception ex) {
               _logger.Error(ex, "Error crítico al guardar la cita");
               _dialogService.ShowError("Se ha producido un error inesperado al guardar la cita.");
          }
     }

     /// <summary>
     /// Cancela la operación y cierra la ventana sin guardar cambios.
     /// </summary>
     [RelayCommand]
     private void Cancel() {
          _logger.Debug("Operación de edición cancelada por el usuario");
          CloseAction?.Invoke(false);
     }
}