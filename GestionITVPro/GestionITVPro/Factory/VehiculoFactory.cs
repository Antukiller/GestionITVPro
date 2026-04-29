using GestionITVPro.Enums;
using GestionITVPro.Models;

namespace GestionITVPro.Factory;
public static class VehiculosFactory {
    public static IEnumerable<Cita> Seed() {
        var hoy = DateTime.Today;
        var mañana = hoy.AddDays(1);
        var pasado = hoy.AddDays(2);
        var proximaSemana = hoy.AddDays(7);

        return new List<Cita> {
            // --- ESCENARIO 1: CUPO MÁXIMO POR DNI (3 citas mismo día) ---
            // Grupo A: 12345678Z (Hoy)
            new Cita { Id = 1, Matricula = "1111-BBB", Marca = "Toyota", Modelo = "Corolla", Cilindrada = 1800, Motor = Motor.Hibrido, DniPropietario = "12345678Z", FechaItv = hoy.AddYears(-4), FechaInspeccion = hoy },
            new Cita { Id = 2, Matricula = "2222-CCC", Marca = "Toyota", Modelo = "Yaris", Cilindrada = 1500, Motor = Motor.Hibrido, DniPropietario = "12345678Z", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy },
            new Cita { Id = 3, Matricula = "3333-DDD", Marca = "Lexus", Modelo = "UX", Cilindrada = 2000, Motor = Motor.Hibrido, DniPropietario = "12345678Z", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy },

            // Grupo B: 23456789D (Mañana)
            new Cita { Id = 4, Matricula = "1001-FFF", Marca = "BMW", Modelo = "M3", Cilindrada = 3000, Motor = Motor.Gasolina, DniPropietario = "23456789D", FechaItv = hoy.AddYears(-3), FechaInspeccion = mañana },
            new Cita { Id = 5, Matricula = "1002-GGG", Marca = "BMW", Modelo = "M4", Cilindrada = 3000, Motor = Motor.Gasolina, DniPropietario = "23456789D", FechaItv = hoy.AddYears(-2), FechaInspeccion = mañana },
            new Cita { Id = 6, Matricula = "1003-HHH", Marca = "BMW", Modelo = "X5", Cilindrada = 3000, Motor = Motor.Diesel, DniPropietario = "23456789D", FechaItv = hoy.AddYears(-1), FechaInspeccion = mañana },

            // Grupo C: 34567890V (Pasado Mañana)
            new Cita { Id = 7, Matricula = "2001-JJJ", Marca = "Tesla", Modelo = "Model S", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "34567890V", FechaItv = hoy.AddYears(-1), FechaInspeccion = pasado },
            new Cita { Id = 8, Matricula = "2002-KKK", Marca = "Tesla", Modelo = "Model X", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "34567890V", FechaItv = hoy.AddYears(-1), FechaInspeccion = pasado },
            new Cita { Id = 9, Matricula = "2003-LLL", Marca = "Tesla", Modelo = "Model Y", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "34567890V", FechaItv = hoy.AddYears(-1), FechaInspeccion = pasado },

            // Grupo D: 45678901G (Hoy)
            new Cita { Id = 10, Matricula = "3001-MMM", Marca = "Audi", Modelo = "A1", Cilindrada = 1000, Motor = Motor.Gasolina, DniPropietario = "45678901G", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy },
            new Cita { Id = 11, Matricula = "3002-NNN", Marca = "Audi", Modelo = "A3", Cilindrada = 1500, Motor = Motor.Diesel, DniPropietario = "45678901G", FechaItv = hoy.AddYears(-3), FechaInspeccion = hoy },
            new Cita { Id = 12, Matricula = "3003-PPP", Marca = "Audi", Modelo = "A4", Cilindrada = 2000, Motor = Motor.Hibrido, DniPropietario = "45678901G", FechaItv = hoy.AddYears(-4), FechaInspeccion = hoy },

            // Grupo E: 56789012B (Próxima semana)
            new Cita { Id = 13, Matricula = "4001-RRR", Marca = "Seat", Modelo = "Ibiza", Cilindrada = 1000, Motor = Motor.Gasolina, DniPropietario = "56789012B", FechaItv = hoy.AddYears(-5), FechaInspeccion = proximaSemana },
            new Cita { Id = 14, Matricula = "4002-SSS", Marca = "Seat", Modelo = "Leon", Cilindrada = 1500, Motor = Motor.Diesel, DniPropietario = "56789012B", FechaItv = hoy.AddYears(-2), FechaInspeccion = proximaSemana },
            new Cita { Id = 15, Matricula = "4003-TTT", Marca = "Seat", Modelo = "Ateca", Cilindrada = 1600, Motor = Motor.Hibrido, DniPropietario = "56789012B", FechaItv = hoy.AddYears(-1), FechaInspeccion = proximaSemana },

            // --- ESCENARIO 2: UNICIDAD DE CITA (Misma matrícula, días distintos) ---
            new Cita { Id = 16, Matricula = "4444-FFF", Marca = "Ford", Modelo = "Focus", Cilindrada = 1600, Motor = Motor.Gasolina, DniPropietario = "44394815T", FechaItv = hoy.AddYears(-5), FechaInspeccion = mañana },
            new Cita { Id = 17, Matricula = "4444-FFF", Marca = "Ford", Modelo = "Focus", Cilindrada = 1600, Motor = Motor.Gasolina, DniPropietario = "44394815T", FechaItv = hoy.AddYears(-5), FechaInspeccion = proximaSemana },
            new Cita { Id = 18, Matricula = "5555-ZZZ", Marca = "Renault", Modelo = "Zoe", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "04494301W", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy },
            new Cita { Id = 19, Matricula = "5555-ZZZ", Marca = "Renault", Modelo = "Zoe", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "04494301W", FechaItv = hoy.AddYears(-1), FechaInspeccion = pasado },
            new Cita { Id = 20, Matricula = "6666-XXX", Marca = "Kia", Modelo = "Niro", Cilindrada = 1600, Motor = Motor.Hibrido, DniPropietario = "53340051Y", FechaItv = hoy.AddYears(-2), FechaInspeccion = mañana },
            new Cita { Id = 21, Matricula = "6666-XXX", Marca = "Kia", Modelo = "Niro", Cilindrada = 1600, Motor = Motor.Hibrido, DniPropietario = "53340051Y", FechaItv = hoy.AddYears(-2), FechaInspeccion = proximaSemana },
            new Cita { Id = 22, Matricula = "7777-WWW", Marca = "Mazda", Modelo = "MX-5", Cilindrada = 2000, Motor = Motor.Gasolina, DniPropietario = "48203521X", FechaItv = hoy.AddYears(-3), FechaInspeccion = hoy.AddDays(5) },
            new Cita { Id = 23, Matricula = "7777-WWW", Marca = "Mazda", Modelo = "MX-5", Cilindrada = 2000, Motor = Motor.Gasolina, DniPropietario = "48203521X", FechaItv = hoy.AddYears(-3), FechaInspeccion = hoy.AddDays(15) },
            new Cita { Id = 24, Matricula = "8888-VVV", Marca = "Nissan", Modelo = "Juke", Cilindrada = 1000, Motor = Motor.Gasolina, DniPropietario = "74945100L", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(10) },
            new Cita { Id = 25, Matricula = "8888-VVV", Marca = "Nissan", Modelo = "Juke", Cilindrada = 1000, Motor = Motor.Gasolina, DniPropietario = "74945100L", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(20) },

            // --- ESCENARIO 3: CASOS GENERALES (26-60) ---
            new Cita { Id = 26, Matricula = "9001-BCG", Marca = "Honda", Modelo = "Civic", Cilindrada = 1500, Motor = Motor.Gasolina, DniPropietario = "09430294C", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(3) },
            new Cita { Id = 27, Matricula = "9002-DFH", Marca = "Citroen", Modelo = "C3", Cilindrada = 1200, Motor = Motor.Gasolina, DniPropietario = "12840921B", FechaItv = hoy.AddYears(-4), FechaInspeccion = hoy.AddDays(4) },
            new Cita { Id = 28, Matricula = "9003-JKL", Marca = "Skoda", Modelo = "Octavia", Cilindrada = 2000, Motor = Motor.Diesel, DniPropietario = "83021943X", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(5) },
            new Cita { Id = 29, Matricula = "9004-MNP", Marca = "Peugeot", Modelo = "2008", Cilindrada = 1200, Motor = Motor.Gasolina, DniPropietario = "48392015K", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(6) },
            new Cita { Id = 30, Matricula = "9005-RST", Marca = "Fiat", Modelo = "500", Cilindrada = 1000, Motor = Motor.Hibrido, DniPropietario = "93021842V", FechaItv = hoy.AddYears(-3), FechaInspeccion = hoy.AddDays(7) },
            new Cita { Id = 31, Matricula = "9006-VWX", Marca = "Volvo", Modelo = "V60", Cilindrada = 2000, Motor = Motor.Diesel, DniPropietario = "02394851G", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(8) },
            new Cita { Id = 32, Matricula = "9007-BYZ", Marca = "Dacia", Modelo = "Logan", Cilindrada = 1000, Motor = Motor.Gasolina, DniPropietario = "19283746A", FechaItv = hoy.AddYears(-5), FechaInspeccion = hoy.AddDays(9) },
            new Cita { Id = 33, Matricula = "9008-DRS", Marca = "Suzuki", Modelo = "Vitara", Cilindrada = 1400, Motor = Motor.Hibrido, DniPropietario = "56473829S", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(10) },
            new Cita { Id = 34, Matricula = "9009-FGH", Marca = "Jaguar", Modelo = "XE", Cilindrada = 2000, Motor = Motor.Diesel, DniPropietario = "10293847H", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(11) },
            new Cita { Id = 35, Matricula = "9010-JKL", Marca = "Mitsubishi", Modelo = "Space Star", Cilindrada = 1200, Motor = Motor.Gasolina, DniPropietario = "88223344W", FechaItv = hoy.AddYears(-4), FechaInspeccion = hoy.AddDays(12) },
            new Cita { Id = 36, Matricula = "9011-MNP", Marca = "Alfa Romeo", Modelo = "Tonale", Cilindrada = 1500, Motor = Motor.Hibrido, DniPropietario = "99001122V", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(13) },
            new Cita { Id = 37, Matricula = "9012-PST", Marca = "Jeep", Modelo = "Wrangler", Cilindrada = 2000, Motor = Motor.Gasolina, DniPropietario = "11882233N", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(14) },
            new Cita { Id = 38, Matricula = "9013-TVW", Marca = "Subaru", Modelo = "Impreza", Cilindrada = 1600, Motor = Motor.Gasolina, DniPropietario = "77665544X", FechaItv = hoy.AddYears(-3), FechaInspeccion = hoy.AddDays(15) },
            new Cita { Id = 39, Matricula = "9014-BCG", Marca = "Land Rover", Modelo = "Defender", Cilindrada = 3000, Motor = Motor.Diesel, DniPropietario = "22334455K", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(16) },
            new Cita { Id = 40, Matricula = "9015-DFH", Marca = "Opel", Modelo = "Mokka", Cilindrada = 1200, Motor = Motor.Electrico, DniPropietario = "66778899R", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(17) },
            new Cita { Id = 41, Matricula = "9016-JKL", Marca = "Cupra", Modelo = "Formentor", Cilindrada = 1500, Motor = Motor.Gasolina, DniPropietario = "12312312R", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(18) },
            new Cita { Id = 42, Matricula = "9017-MNP", Marca = "Mini", Modelo = "Countryman", Cilindrada = 1500, Motor = Motor.Gasolina, DniPropietario = "45645645M", FechaItv = hoy.AddYears(-3), FechaInspeccion = hoy.AddDays(19) },
            new Cita { Id = 43, Matricula = "9018-RST", Marca = "Lancia", Modelo = "Ypsilon", Cilindrada = 1200, Motor = Motor.Hibrido, DniPropietario = "78978978E", FechaItv = hoy.AddYears(-6), FechaInspeccion = hoy.AddDays(20) },
            new Cita { Id = 44, Matricula = "9019-VWX", Marca = "Smart", Modelo = "#1", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "14725836F", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(21) },
            new Cita { Id = 45, Matricula = "9020-BYZ", Marca = "Polestar", Modelo = "2", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "36925814R", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(22) },
            new Cita { Id = 46, Matricula = "9021-DRS", Marca = "DS", Modelo = "DS7", Cilindrada = 1600, Motor = Motor.Hibrido, DniPropietario = "25814736Y", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(23) },
            new Cita { Id = 47, Matricula = "9022-FGH", Marca = "Infiniti", Modelo = "Q50", Cilindrada = 2200, Motor = Motor.Diesel, DniPropietario = "15975346X", FechaItv = hoy.AddYears(-4), FechaInspeccion = hoy.AddDays(24) },
            new Cita { Id = 48, Matricula = "9023-JKL", Marca = "Cadillac", Modelo = "Lyriq", Cilindrada = 0, Motor = Motor.Electrico, DniPropietario = "75315946L", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(25) },
            new Cita { Id = 49, Matricula = "9024-MNP", Marca = "Chevrolet", Modelo = "Camaro", Cilindrada = 3000, Motor = Motor.Gasolina, DniPropietario = "95175346P", FechaItv = hoy.AddYears(-5), FechaInspeccion = hoy.AddDays(26) },
            new Cita { Id = 50, Matricula = "9025-RST", Marca = "Dodge", Modelo = "Challenger", Cilindrada = 3000, Motor = Motor.Gasolina, DniPropietario = "35715946W", FechaItv = hoy.AddYears(-3), FechaInspeccion = hoy.AddDays(27) },
            new Cita { Id = 51, Matricula = "9026-VWX", Marca = "MG", Modelo = "ZS", Cilindrada = 1500, Motor = Motor.Gasolina, DniPropietario = "45612378D", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(28) },
            new Cita { Id = 52, Matricula = "9027-BYZ", Marca = "SsangYong", Modelo = "Korando", Cilindrada = 1600, Motor = Motor.Diesel, DniPropietario = "78945612S", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(29) },
            new Cita { Id = 53, Matricula = "9028-DRS", Marca = "Lotus", Modelo = "Emira", Cilindrada = 2000, Motor = Motor.Gasolina, DniPropietario = "12378945M", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(30) },
            new Cita { Id = 54, Matricula = "9029-FGH", Marca = "McLaren", Modelo = "Artura", Cilindrada = 3000, Motor = Motor.Hibrido, DniPropietario = "14736925B", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(1) },
            new Cita { Id = 55, Matricula = "9030-JKL", Marca = "Ferrari", Modelo = "296 GTB", Cilindrada = 3000, Motor = Motor.Hibrido, DniPropietario = "25836914Q", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(2) },
            new Cita { Id = 56, Matricula = "9031-MNP", Marca = "Lamborghini", Modelo = "Urus", Cilindrada = 3000, Motor = Motor.Gasolina, DniPropietario = "36914725T", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(3) },
            new Cita { Id = 57, Matricula = "9032-RST", Marca = "Bentley", Modelo = "Bentayga", Cilindrada = 3000, Motor = Motor.Hibrido, DniPropietario = "74185296Y", FechaItv = hoy.AddYears(-2), FechaInspeccion = hoy.AddDays(4) },
            new Cita { Id = 58, Matricula = "9033-VWX", Marca = "Rolls-Royce", Modelo = "Cullinan", Cilindrada = 3000, Motor = Motor.Gasolina, DniPropietario = "85296314S", FechaItv = hoy.AddYears(-3), FechaInspeccion = hoy.AddDays(5) },
            new Cita { Id = 59, Matricula = "9034-BYZ", Marca = "Aston Martin", Modelo = "DBX", Cilindrada = 3000, Motor = Motor.Gasolina, DniPropietario = "96325814D", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(6) },
            new Cita { Id = 60, Matricula = "9035-DRS", Marca = "Maserati", Modelo = "Grecale", Cilindrada = 2000, Motor = Motor.Hibrido, DniPropietario = "15935724K", FechaItv = hoy.AddYears(-1), FechaInspeccion = hoy.AddDays(7) }
        };
    }
}
