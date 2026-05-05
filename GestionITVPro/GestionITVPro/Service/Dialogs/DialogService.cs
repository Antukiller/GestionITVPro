using System.Windows;

namespace GestionITVPro.Service.Dialogs;

public class DialogService : IDialogService {
    public void ShowError(string message, string title = "Error") {
        // Usamos MessageBoxImage.Error para el icono de la cruz roja
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowSuccess(string message, string title = "Éxito") {
        // WPF no tiene un icono de "Check", se suele usar Information para éxito
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title = "Advertencia") {
        // Usamos MessageBoxImage.Warning para el triángulo amarillo
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowInfo(string message, string title = "Información") {
        // Usamos MessageBoxImage.Information para el círculo azul con la "i"
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public bool ShowConfirmation(string message, string title = "Confirmar") {
        // Usamos MessageBoxImage.Question para el signo de interrogación
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) 
               == MessageBoxResult.Yes;
    }
}