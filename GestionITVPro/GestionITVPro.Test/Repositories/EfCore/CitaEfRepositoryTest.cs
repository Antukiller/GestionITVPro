using FluentAssertions;
using GestionITVPro.Entity;
using GestionITVPro.Enums;
using GestionITVPro.Error.Cita;
using GestionITVPro.Models;
using GestionITVPro.Repositories.EfCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GestionITVPro.Test.Repositories.EfCore;

/// <summary>
/// Tests para PersonasEfRepository.
/// Configuración de tests: Se utiliza SQLite en memoria (:memory:) para cada clase de test.
/// La conexión se mantiene abierta durante toda la clase para compartir la BD en memoria.
/// 
/// Parámetros de configuración:
/// - dropData: true  → Borra cualquier dato existente al inicio (BD limpia)
/// - seedData: false → No carga datos de semilla (datos de prueba)
/// 
/// Esta configuración es ideal para tests porque:
/// 1. SQLite real en memoria (más realista que InMemory)
/// 2. Soporta foreign keys, transacciones, SQL real
/// 3. Cada clase de test tiene su propia BD independiente
/// 4. Los IDs siempre empiezan desde 1 en cada test
/// </summary>

[TestFixture]
public class CitaEfRepositoryTests {
    [TestFixture]
    public class CasosPositivos {
        private SqliteConnection _connection = null!;

        [SetUp]
        public void SetUp() {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();
            _repository = new CitaEfRepository(_context, dropData: true, seedData: false);
        }

        [TearDown]
        public void TearDown() {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        private AppDbContext _context = null!;
        private CitaEfRepository _repository = null!;
        
        
         [Test]
        public void Create_VehiculoValido_CrearCorrectamente() {
            // Arrange 
            var v = new Cita {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            
            // Act 
            var r = _repository.Create(v);

            r.IsSuccess.Should().BeTrue();
            r.Value.Id.Should().Be(1);
        }

        [Test]
        public void GetById_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Cita {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.GetById(1);
            
            // Assert
            r.Should().NotBeNull();
            r!.Id.Should().Be(1);
        }

        [Test]
        public void GetByMatricula_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Cita {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.GetByMatricula("1234-BBB");
            
            // Assert
            r.Should().NotBeNull();
            r!.Matricula.Should().Be("1234-BBB");
        }
        
        [Test]
        public void GetByDniPropietario_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Cita {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.GetByDniPropietario("12345678A");
            
            // Assert
            r.Should().NotBeNull();
            r!.DniPropietario.Should().Be("12345678A");
        }

        [Test]
        public void ExistsDniPropietario_CuandoExiste_RetornarTrue() {
            // Arrange
            var v = new Cita {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.ExistsDniPropietario("12345678A");
            
            // Assert
            r.Should().BeTrue();
        }
        
        [Test]
        public void ExistsMatricula_CuandoExiste_RetornarTrue() {
            // Arrange
            var v = new Cita {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.ExistsMatricula("1234-BBB");
            
            // Assert
            r.Should().BeTrue();
        }

        [Test]
        public void GetAll_SinParametros_RetonarTodos() {
            _repository.Create(new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            });
            
            _repository.Create(new Cita {
                Matricula = "1234-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 1500, Motor = Motor.Diesel,
                DniPropietario = "12345678B"
            });
            
            // Act 
            var r = _repository.GetAll();
            
            // Assert
            r.Should().HaveCount(2);
        }

        [Test]
        // Arrange 
        public void GetAll_ConPaginacion_RetornarPagina() {
            // Arrange 
            for (var i = 1; i <= 5; i++)
                _repository.Create(new Cita {
                    Matricula = $"{i:D4}-BBB", Marca = $"Marca{i}", Modelo = "Modelo", Cilindrada = 3000,
                    Motor = Motor.Diesel, DniPropietario = $"{i:D8}A"
                });
            
            // Act
            var r = _repository.GetAll(1, 3);
            
            // Assert
            r.Should().HaveCount(3);
        }

        [Test]
        public void GetAll_SinIncluirBorrados_RetornarSoloActivos() {
            // Arrange
            _repository.Create(new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            });
            
            var v2 = _repository.Create(new Cita {
                Matricula = "1234-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 1500, Motor = Motor.Diesel,
                DniPropietario = "12345678B"
            }).Value;
            _repository.Delete(v2.Id);
            
            // Act
            var r = _repository.GetAll(includeDeleted: false);
            
            // Assert
            r.Should().HaveCount(1);
            r.First().Matricula.Should().Be("1234-BBB");
        }

        [Test]
        public void Update_ConDatosValidos_RetornarActualizacion() {
            // Arrange
            var v = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            _repository.Create(v);

            var a = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-467", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            
            // Act
            var r = _repository.Update(1, a);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            r.Value.Modelo.Should().Be("M-467");
        }
        
        [Test]
        public void Update_CambiarModeloSinCambiarFecha_DebeTenerExito()
        {
            // Arrange
            var fecha = new DateTime(2026, 10, 15);
            var original = new Cita { Matricula = "9999XXX", FechaItv = fecha, DniPropietario = "12345678Z", Marca = "Ford", Modelo = "Fiesta" };
            var creada = _repository.Create(original).Value;

            // Act: Editamos el modelo manteniendo la misma fecha
            var editada = creada with { Modelo = "Focus" };
            var result = _repository.Update(creada.Id, editada);

            // Assert: No debe colisionar con sigo misma
            result.IsSuccess.Should().BeTrue();
            result.Value.Modelo.Should().Be("Focus");
        }

        [Test]
        public void Delete_Logico_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            _repository.Create(v);
            
            // Act
            var r = _repository.Delete(1);
            
            // Assert
            r.Should().NotBeNull();
            r!.IsDeleted.Should().BeTrue();
        }

        [Test]
        public void Delete_Fisico_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            _repository.Create(v);
            
            // Act
            var r = _repository.Delete(1, false);
            
            // Assert
            r.Should().NotBeNull();
            _repository.GetById(1).Should().BeNull();
        }

        [Test]
        public void DeleteAll_CuandoHayDatos_EliminarTodos() {
            // Arrange
            _repository.Create(new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            });
            
            _repository.Create(new Cita {
                Matricula = "1234-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 1500, Motor = Motor.Diesel,
                DniPropietario = "12345678B"
            });
            
            // Act
            var r = _repository.DeleteAll();
            
            // Assert
            r.Should().BeTrue();
            _repository.GetAll().Should().BeEmpty();
        }
        
        [Test]
        public void Restore_DebeRecuperarCitaYMantenerFecha()
        {
            // Arrange
            var fecha = new DateTime(2027, 01, 01);
            var cita = _repository.Create(new Cita { Matricula = "RECOV1", FechaItv = fecha, DniPropietario = "X", Marca = "X", Modelo = "X" }).Value;
            _repository.Delete(cita.Id, isLogical: true);

            // Act
            var result = _repository.Restore(cita.Id);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.IsDeleted.Should().BeFalse();
            result.Value.FechaItv.Should().Be(fecha);
        }
    }
    }


    [TestFixture]
    public class CasosNegativos {
        private SqliteConnection _connection = null!;

        [SetUp]
        public void SetUp() {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();
            _repository = new CitaEfRepository(_context, dropData: true, seedData: false);
        }

        [TearDown]
        public void TearDown() {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        private AppDbContext _context = null!;
        private CitaEfRepository _repository = null!;
        
        [Test]
        public void Create_ConMatriculaExistente_RetornarFailure() {
            var v1 = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            var v2 = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-6", Cilindrada = 3000, Motor = Motor.Hibrido,
                DniPropietario = "12345678B"
            };
            _repository.Create(v1);
            
            // Act 
            var r = _repository.Create(v2);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.MatriculaAlreadyExists>();
            (r.Error as CitaError.MatriculaAlreadyExists)?.Matricula.Should().Be("1234-BBB");
            r.Error.Message.Should().Contain("1234-BBB");
        }
        
        [Test]
        public void Create_CitaDuplicadaMismoDia_DebeRetornarError()
        {
            // Arrange
            var fecha = new DateTime(2026, 10, 15);
            var cita1 = new Cita { Matricula = "1234BBB", FechaItv = fecha, DniPropietario = "12345678Z", Marca = "Toyota", Modelo = "Corolla" };
            _repository.Create(cita1);

            // Act: Intentar crear otra para el mismo coche el mismo día
            var cita2 = cita1 with { Id = 0, Marca = "Otro" }; 
            var result = _repository.Create(cita2);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Contain("La matrícula 1234BBB ya tiene programada una cita para esa fecha.");
        }
        
        [Test]
        public void Create_ExcederLimiteDeTresVehiculos_DebeRetornarError()
        {
            // Arrange
            const string dni = "11112222A";
            for (int i = 1; i <= 3; i++)
            {
                _repository.Create(new Cita 
                { 
                    Matricula = $"MAT00{i}", DniPropietario = dni, 
                    FechaItv = DateTime.Now.AddDays(i), Marca = "Fiat", Modelo = "500" 
                });
            }

            // Act: Intentar el cuarto
            var cuarta = new Cita { Matricula = "MAT004", DniPropietario = dni, FechaItv = DateTime.Now.AddDays(10), Marca = "Fiat", Modelo = "Panda" };
            var result = _repository.Create(cuarta);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Contain("límite de 3 vehículos");
        }
        
        [Test]
        public void Create_CuandoPropietarioYaTiene3_RetornarFailure() {
            // Arrange: Creamos 3 vehículos para el mismo DNI
            var dni = "12345678X";
            for (int i = 1; i <= 3; i++) {
                _repository.Create(new Cita {
                    Matricula = $"TEST-{i}", Marca = "A", Modelo = "B",
                    Cilindrada = 100, Motor = Motor.Gasolina, DniPropietario = dni
                });
            }

            // Act: Intentamos crear el cuarto
            var v4 = new Cita {
                Matricula = "TEST-4", Marca = "A", Modelo = "B",
                Cilindrada = 100, Motor = Motor.Gasolina, DniPropietario = dni
            };
            var r = _repository.Create(v4);

            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.Validation>();
            r.Error.Message.Should().Contain("límite de 3 vehículos");
        }

        [Test]
        public void GetById_CuandoNoExiste_RetornarNull() {
            // Arrange & Act
            var r = _repository.GetById(213456);
            
            // Assert
            r.Should().BeNull();
            
        }
        
        [Test]
        public void GetByDniPropietario_CuandoNoExiste_RetonarNull() {
            // Arrange & Act
            var r = _repository.GetByDniPropietario("12345678asd");
            
            // Assert
            r.Should().BeNull();
        }

        [Test]
        public void GetByMatricula_CuandoNoExiste_RetornarNull() {
            // Arrange & Act
            var r = _repository.GetByMatricula("1234456-BVCV");
            
            // Assert
            r.Should().BeNull();
        }

        [Test]
        public void ExistsDniPropietario_CuandoNoExiste_RetornarFalse() {
            // Arrange & Act
            var r = _repository.ExistsDniPropietario("1234567890DC");
            
            // Assert
            r.Should().BeFalse();
        }

        [Test]
        public void ExistsMatricula_CuandoNoExiste_RetornarFailure() {
            // Arrange & Act
            var r = _repository.ExistsMatricula("12345-VBNM");
            
            // Assert
            r.Should().BeFalse();
        }

        [Test]
        public void Update_CuandoNoExiste_RetornarFailure() {
            // Arrange
            var v1 = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            // Act
            var r = _repository.Update(9999, v1);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.NotFound>();
            (r.Error as CitaError.NotFound)?.Id.Should().Be("9999");
            r.Error.Message.Should().Contain("9999");

        }
        
        [Test]
        public void Update_ConMatriculaExistente_RetornarFailure() { 
            // Arrange
            var v1 = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            var v2 = new Cita {
                Matricula = "1234-BBC", Marca = "BMW", Modelo = "M-6", Cilindrada = 3000, Motor = Motor.Hibrido,
                DniPropietario = "12345678B"
            };
            _repository.Create(v1);
            _repository.Create(v2);
            
            // Act
            var r = _repository.Update(2, v1);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.MatriculaAlreadyExists>();
            (r.Error as CitaError.MatriculaAlreadyExists)?.Matricula.Should().Be("1234-BBB");
        }
        
        [Test]
        public void Update_ConDniPropietarioExisteEnOtro_RetornarFaulure() {
            // Arrange
            var v1 = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            var v2 = new Cita {
                Matricula = "1234-BBC", Marca = "BMW", Modelo = "M-6", Cilindrada = 3000, Motor = Motor.Hibrido,
                DniPropietario = "12345678B"
            };
            _repository.Create(v1);
            _repository.Create(v2);
            
            var a = new Cita {
                Matricula = "1234-BBC", Marca = "BMW", Modelo = "M-6", Cilindrada = 3000, Motor = Motor.Hibrido,
                DniPropietario = "12345678A"
            };
            
            // Act 
            var r = _repository.Update(2, a);

            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.DniPropiestarioAlreadyExists>();
            (r.Error as CitaError.DniPropiestarioAlreadyExists)?.DniPropietario.Should().Be("12345678A");
        }

        [Test]
        public void Delete_CuandoNoExiste_RetornarNull() {
            // Arrange & Act
            var r = _repository.Delete(999);
            
            // Assert
            r.Should().BeNull();
        }

        [Test]
        public void Restore_CuandoNoExiste_RetornarFailure() {
            // Arrange & Act
            var r = _repository.Restore(999);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.NotFound>();
        }

    }

    [TestFixture]
    public class CasosMixtos {
        private SqliteConnection _connection = null!;

        [SetUp]
        public void SetUp() {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();
            _repository = new CitaEfRepository(_context, dropData: true, seedData: false);
        }

        [TearDown]
        public void TearDown() {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        private AppDbContext _context = null!;
        private CitaEfRepository _repository = null!;
        
        
        [Test]
        public void Restore_CuandoEliminasLogicamente_Restaurar() {
            // Arrange
            var v1 = new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            var creada = _repository.Create(v1).Value;
            _repository.Delete(creada.Id);
            
            // Act
            var r = _repository.Restore(creada.Id);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            r.Value.IsDeleted.Should().BeFalse();
            r.Value.DeletedAt.Should().BeNull();
        }

        [Test]
        public void CountVehiculos_SinEliminados_RetornarSoloActivos() {
            // Arrange (3 vehículos creados)
            _repository.Create(new Cita { Matricula = "V1", DniPropietario = "A", Marca="A", Modelo="A" });
            var v2 = _repository.Create(new Cita { Matricula = "V2", DniPropietario = "B", Marca="A", Modelo="A" }).Value;
            _repository.Create(new Cita { Matricula = "V3", DniPropietario = "C", Marca="A", Modelo="A" });
    
            // Borramos 1
            _repository.Delete(v2.Id);

            // Act
            var r = _repository.CountCita();
    
            // Assert: Quedan 2 activos
            r.Should().Be(2); 
        }

        [Test]
        public void CountVehiculos_IncluyendoEliminados_ContarTodos() {
            _repository.Create(new Cita { Matricula = "V1", DniPropietario = "A", Marca = "A", Modelo = "A" });
            var v2 = _repository.Create(new Cita
                { Matricula = "V2", DniPropietario = "B", Marca = "A", Modelo = "A" }).Value;
            _repository.Delete(v2.Id);
            
            // Act
            var r = _repository.CountCita(true);
            
            // Assert
            r.Should().Be(2);
        }

        public void DeleteAll_DeberiaVaciarRepositorio() {
            // Arrange
            _repository.Create(new Cita {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            });
            
            _repository.Create(new Cita {
                Matricula = "1234-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 1500, Motor = Motor.Diesel,
                DniPropietario = "12345678B"
            });
            
            // Act
            var r = _repository.DeleteAll();
            
            // Assert
            r.Should().BeTrue();
            _repository.GetAll().Should().BeEmpty();
        }
    }


