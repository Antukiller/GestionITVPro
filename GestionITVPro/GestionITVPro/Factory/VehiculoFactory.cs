using GestionITVPro.Enums;
using GestionITVPro.Models;

namespace GestionITVPro.Factory;

/// <summary>
/// Factoría con datos semilla para 30 registros de Vehículos.
/// </summary>
public static class VehiculosFactory {
    public static IEnumerable<Cita> Seed() {
        return new List<Cita> {
            // Bloque 1
            new Cita { Id = 1, Matricula = "1234-BCG", Marca = "Toyota", Modelo = "Corolla", Cilindrada = 1800, Motor = Motor.Hibrido, DniPropietario = "12345678Z" },
            new Cita { Id = 2, Matricula = "5678-DFH", Marca = "Volkswagen", Modelo = "Golf", Cilindrada = 2000, Motor = Motor.Diesel, DniPropietario = "23456789D" },
            new Cita { Id = 3, Matricula = "9012-JKL", Marca = "Tesla", Modelo = "Model 3", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "34567890V" },
            new Cita { Id = 4, Matricula = "3456-MNP", Marca = "BMW", Modelo = "Serie 3", Cilindrada = 2500, Motor = Motor.Gasolina, DniPropietario = "45678901G" },
            new Cita { Id = 5, Matricula = "7890-RST", Marca = "Audi", Modelo = "A4", Cilindrada = 2000, Motor = Motor.Diesel, DniPropietario = "56789012B" },
            new Cita { Id = 6, Matricula = "1122-VWX", Marca = "Ford", Modelo = "Focus", Cilindrada = 1600, Motor = Motor.Gasolina, DniPropietario = "11111111H" },
            new Cita { Id = 7, Matricula = "3344-BYZ", Marca = "Mercedes", Modelo = "Clase C", Cilindrada = 2200, Motor = Motor.Diesel, DniPropietario = "22222222J" },
            new Cita { Id = 8, Matricula = "5566-DRS", Marca = "Renault", Modelo = "Clio", Cilindrada = 1200, Motor = Motor.Gasolina, DniPropietario = "33333333P" },
            new Cita { Id = 9, Matricula = "7788-FGH", Marca = "Hyundai", Modelo = "Ioniq", Cilindrada = 1600, Motor = Motor.Hibrido, DniPropietario = "44444444A" },
            new Cita { Id = 10, Matricula = "9900-JKL", Marca = "Kia", Modelo = "EV6", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "55555555K" },

            // Bloque 2
            new Cita { Id = 11, Matricula = "1212-MNP", Marca = "Seat", Modelo = "Ibiza", Cilindrada = 1000, Motor = Motor.Gasolina, DniPropietario = "66666666Q" },
            new Cita { Id = 12, Matricula = "3434-PST", Marca = "Peugeot", Modelo = "3008", Cilindrada = 1500, Motor = Motor.Diesel, DniPropietario = "77777777B" },
            new Cita { Id = 13, Matricula = "5656-TVW", Marca = "Mazda", Modelo = "CX-5", Cilindrada = 2000, Motor = Motor.Gasolina, DniPropietario = "88888888Y" },
            new Cita { Id = 14, Matricula = "7878-BCG", Marca = "Nissan", Modelo = "Leaf", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "99999999R" },
            new Cita { Id = 15, Matricula = "9090-DFH", Marca = "Volvo", Modelo = "XC40", Cilindrada = 1500, Motor = Motor.Hibrido, DniPropietario = "00000000T" },
            new Cita { Id = 16, Matricula = "2468-JKL", Marca = "Skoda", Modelo = "Octavia", Cilindrada = 1600, Motor = Motor.Diesel, DniPropietario = "12345678Z" },
            new Cita { Id = 17, Matricula = "1357-MNP", Marca = "Honda", Modelo = "Civic", Cilindrada = 1500, Motor = Motor.Gasolina, DniPropietario = "23456789D" },
            new Cita { Id = 18, Matricula = "8642-RST", Marca = "Lexus", Modelo = "UX", Cilindrada = 2000, Motor = Motor.Hibrido, DniPropietario = "34567890V" },
            new Cita { Id = 19, Matricula = "9753-VWX", Marca = "Porsche", Modelo = "Taycan", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "45678901G" },
            new Cita { Id = 20, Matricula = "2233-BYZ", Marca = "Citroen", Modelo = "C3", Cilindrada = 1200, Motor = Motor.Gasolina, DniPropietario = "56789012B" },

            // Bloque 3
            new Cita { Id = 21, Matricula = "4455-DFH", Marca = "Dacia", Modelo = "Sandero", Cilindrada = 1000, Motor = Motor.Gasolina, DniPropietario = "11111111H" },
            new Cita { Id = 22, Matricula = "6677-JKL", Marca = "Fiat", Modelo = "500e", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "22222222J" },
            new Cita { Id = 23, Matricula = "8899-MNP", Marca = "Mini", Modelo = "Cooper", Cilindrada = 1500, Motor = Motor.Gasolina, DniPropietario = "33333333P" },
            new Cita { Id = 24, Matricula = "1010-PST", Marca = "Opel", Modelo = "Corsa", Cilindrada = 1200, Motor = Motor.Diesel, DniPropietario = "44444444A" },
            new Cita { Id = 25, Matricula = "2020-TVW", Marca = "Mitsubishi", Modelo = "ASX", Cilindrada = 1600, Motor = Motor.Hibrido, DniPropietario = "55555555K" },
            new Cita { Id = 26, Matricula = "3030-BCG", Marca = "Jaguar", Modelo = "I-Pace", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "66666666Q" },
            new Cita { Id = 27, Matricula = "4040-DFH", Marca = "Land Rover", Modelo = "Evoque", Cilindrada = 2000, Motor = Motor.Diesel, DniPropietario = "77777777B" },
            new Cita { Id = 28, Matricula = "5050-JKL", Marca = "Subaru", Modelo = "XV", Cilindrada = 2000, Motor = Motor.Hibrido, DniPropietario = "88888888Y" },
            new Cita { Id = 29, Matricula = "6060-MNP", Marca = "Alfa Romeo", Modelo = "Giulia", Cilindrada = 2200, Motor = Motor.Diesel, DniPropietario = "99999999R" },
            new Cita { Id = 30, Matricula = "7070-RST", Marca = "Jeep", Modelo = "Renegade", Cilindrada = 1300, Motor = Motor.Gasolina, DniPropietario = "00000000T" }
        };
    }
}
