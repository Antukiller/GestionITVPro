using FluentAssertions;
using GestionITVPro.Enums;
using GestionITVPro.Error.Vehiculo;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Json;

namespace GestionITVPro.Test.Repositories.Json;

/// <summary>
/// Tests para PersonasJsonRepository.
/// Configuración de tests: Se utiliza un archivo temporal JSON para cada clase de test.
/// 
/// El repositorio JSON es útil para tests de integración porque:
///
/// - Persiste datos en formato JSON (formato legible)
/// - Se puede inspeccionar el archivo resultante
/// - Simula el comportamiento real del almacenamiento
/// 
/// Parámetros de configuración del constructor:
/// - new PersonasJsonRepository(_tempFile, dropData: false, seedData: false)
///   - dropData: false → No borra el archivo al inicio (lo crea nuevo)
///   - seedData: false → No carga datos de semilla
/// 
/// Esta configuración garantiza:
/// 1. Archivo limpio en cada test (usa Path.GetTempFileName())
/// 2. Los IDs siempre empiezan desde 1
/// 3. Tests independientes entre sí
/// 4. Posibilidad de verificar el archivo JSON resultante
/// </summary>
[TestFixture]
public class VehiculoJsonRepositoryTest {
    [SetUp]
    public void SetUp() {
        _tempFile = Path.GetTempFileName();
    }

    [TearDown]
    public void TearDown() {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    private string _tempFile = null!;
    
    [TestFixture]
    public class CasosPositivos {
        [SetUp]
        public void SetUp() {
            _tempFile = Path.GetTempFileName();
            _repository = new VehiculoJsonRepository(_tempFile);
            _repository.DeleteAll();
        }
        
        
        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }
        
        private string _tempFile = null!;
        private VehiculoJsonRepository _repository = null!;
        
        
         [Test]
        public void Create_VehiculoValido_CrearCorrectamente() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BCG",
                Marca = "Toyota",
                Modelo = "Corolla",
                Cilindrada = 1800,
                Motor = Motor.Hibrido,
                DniPropietario = "12345678Z"
            };
            
            // Act
            var resultado = _repository.Create(v);
            
            // Assert
            resultado.IsSuccess.Should().BeTrue(); // Corregido: antes tenías IsFailure
            resultado.Value.Id.Should().Be(1);
            resultado.Value.Matricula.Should().Be("1234-BCG");
        }

        [Test]
        public void GetById_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BCG", Marca = "Toyota", Modelo = "Corolla", 
                Cilindrada = 1800, Motor = Motor.Hibrido, DniPropietario = "12345678Z"
            };
            _repository.Create(v);

            // Act
            var resultado = _repository.GetById(1);
            
            // Assert
            resultado.Should().NotBeNull();
            resultado!.Id.Should().Be(1);
            resultado.Matricula.Should().Be("1234-BCG");
        }

        [Test]
        public void GetByMatricula_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BCG", Marca = "Toyota", Modelo = "Corolla", 
                Cilindrada = 1800, Motor = Motor.Hibrido, DniPropietario = "12345678Z"
            };
            _repository.Create(v);

            // Act
            var resultado = _repository.GetByMatricula("1234-BCG");
            
            // Assert
            resultado.Should().NotBeNull();
            resultado!.Matricula.Should().Be("1234-BCG");
        }

        [Test]
        public void GetByDniPropietario_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var dni = "12345678Z";
            var v = new Vehiculo {
                Matricula = "1234-BCG", Marca = "Toyota", Modelo = "Corolla", 
                Cilindrada = 1800, Motor = Motor.Hibrido, DniPropietario = dni
            };
            _repository.Create(v);

            // Act
            var resultado = _repository.GetByDniPropietario(dni);
            
            // Assert
            resultado.Should().NotBeNull();
            resultado!.DniPropietario.Should().Be(dni);
        }

        [Test]
        public void ExistDniPropietario_CuandoExiste_RetornarTrue() {
            // Arrange
            var dni = "12345678Z";
            var v = new Vehiculo {
                Matricula = "1234-BCG", Marca = "Toyota", Modelo = "Corolla", 
                Cilindrada = 1800, Motor = Motor.Hibrido, DniPropietario = dni
            };
            _repository.Create(v);

            // Act
            var resultado = _repository.ExistsDniPropietario(dni);
            
            // Assert
            resultado.Should().BeTrue();
        }

        [Test]
        public void ExistMatricula_CuandoExiste_RetornarTrue() {
            // Arrange
            var mat = "1234-BCG";
            _repository.Create(new Vehiculo {
                Matricula = mat, Marca = "Toyota", Modelo = "Corolla", 
                Cilindrada = 1800, Motor = Motor.Hibrido, DniPropietario = "12345678Z"
            });

            // Act
            var resultado = _repository.ExistsMatricula(mat);
            
            // Assert
            resultado.Should().BeTrue();
        }

        [Test]
        public void GetAll_SinParametros_RetornarTodos() {
            // Arrange
            _repository.Create(new Vehiculo { Matricula = "1111-BBB", DniPropietario = "1Z", Marca="A", Modelo="A" });
            _repository.Create(new Vehiculo { Matricula = "2222-CCC", DniPropietario = "2Z", Marca="B", Modelo="B" });
            
            // Act
            var resultado = _repository.GetAll();
            
            // Assert
            resultado.Should().HaveCount(2);
        }

        [Test]
        public void GetAll_ConPaginacion_RetornarPagina() {
            // Arrange
            for (var i = 1; i <= 5; i++)
                _repository.Create(new Vehiculo {
                    Matricula = $"{i:D4}-BCG", Marca = "Marca", Modelo = "Modelo", 
                    Cilindrada = 2000, Motor = Motor.Diesel, DniPropietario = $"{i}Z"
                });
            
            // Act
            var resultado = _repository.GetAll(1, 3);
            
            // Assert
            resultado.Should().HaveCount(3);
        }

        [Test]
        public void GetAll_SinIncluirBorrados_RetonarSoloActivos() {
            // Arrange
            _repository.Create(new Vehiculo { Matricula = "1111-AAA", DniPropietario = "1Z", Marca="A", Modelo="A" });
            var res2 = _repository.Create(new Vehiculo { Matricula = "2222-BBB", DniPropietario = "2Z", Marca="B", Modelo="B" });
            
            _repository.Delete(res2.Value.Id); // Borrado lógico por defecto
            
            // Act 
            var resultado = _repository.GetAll(includeDeleted: false);
            
            // Assert
            resultado.Should().HaveCount(1);
            resultado.First().Matricula.Should().Be("1111-AAA");
        }

        [Test]
        public void Update_ConDatosValidosActualizar() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BCG", Marca = "Toyota", Modelo = "Corolla", 
                Cilindrada = 1800, Motor = Motor.Hibrido, DniPropietario = "12345678Z"
            };
            var creado = _repository.Create(v).Value;

            var a = creado with { Marca = "Ferrari" };

            // Act
            var resultado = _repository.Update(creado.Id, a);
            
            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Marca.Should().Be("Ferrari"); // Corregido: antes comparabas objeto con string
        }

        [Test]
        public void Delete_Logico_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var res = _repository.Create(new Vehiculo { Matricula = "1234-BCG", DniPropietario = "1Z", Marca="A", Modelo="A" });
            
            // Act
            var resultado = _repository.Delete(res.Value.Id);
            
            // Assert
            resultado.Should().NotBeNull();
            resultado!.IsDeleted.Should().BeTrue();
        }
        
        [Test]
        public void Delete_Fisico_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var res = _repository.Create(new Vehiculo { Matricula = "1234-BCG", DniPropietario = "1Z", Marca="A", Modelo="A" });
            int id = res.Value.Id;

            // Act
            var resultado = _repository.Delete(id, false);
            
            // Assert
            resultado.Should().NotBeNull();
            _repository.GetById(id).Should().BeNull();
        }

        [Test]
        public void DeleteAll_CuandoHayDatos_EliminarTodos() {
            // Arrange
            _repository.Create(new Vehiculo { Matricula = "1111-AAA", DniPropietario = "1Z", Marca="A", Modelo="A" });
            _repository.Create(new Vehiculo { Matricula = "2222-BBB", DniPropietario = "2Z", Marca="B", Modelo="B" });
            
            // Act
            var resultado = _repository.DeleteAll();
            
            // Assert
            resultado.Should().BeTrue();
            _repository.GetAll().Should().BeEmpty();
        }
    }
    
    [TestFixture]
    public class CasosNegativos {
        [SetUp]
        public void SetUp() {
            _tempFile = Path.GetTempFileName();
            _repository = new VehiculoJsonRepository(_tempFile);
            _repository.DeleteAll();
        }
        
        
        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }
        
        private string _tempFile = null!;
        private VehiculoJsonRepository _repository = null!;
        
         [Test]
        public void Create_ConMatriculaExistente_RetornarFailure() {
            // Arrange
            var v1 = new Vehiculo {
                Id = 2, Matricula = "5678-DFH", Marca = "Volkswagen", Modelo = "Golf", Cilindrada = 2000,
                Motor = Motor.Diesel, DniPropietario = "23456789D"
            };
            var v2 = new Vehiculo {
                Id = 3, Matricula = "5678-DFH", Marca = "Tesla", Modelo = "Model 3", Cilindrada = 0,
                Motor = Motor.Electrico, DniPropietario = "34567890V"
            };

            _repository.Create(v1);

            var result = _repository.Create(v2);

            result.IsFailure.Should().BeTrue();
            result.Error.Should().BeOfType<VehiculoError.MatriculaAlreadyExists>();
            (result.Error as VehiculoError.MatriculaAlreadyExists)?.Matricula.Should().Be("5678-DFH");
        }

        [Test]
        public void Create_CuandoSuperaLimiteDe3Vehiculos_RetornarFailure() {
            // Arrange
            var dni = "23456789D";
            // Creamos los 3 primeros (el cupo máximo)
            _repository.Create(new Vehiculo { Matricula = "1111-AAA", DniPropietario = dni, Marca="A", Modelo="A" });
            _repository.Create(new Vehiculo { Matricula = "2222-BBB", DniPropietario = dni, Marca="B", Modelo="B" });
            _repository.Create(new Vehiculo { Matricula = "3333-CCC", DniPropietario = dni, Marca="C", Modelo="C" });

            var v4 = new Vehiculo {
                Matricula = "4444-DDD", 
                Marca = "Tesla", 
                Modelo = "Model 3", 
                DniPropietario = dni // <--- Este es el 4º vehículo para el mismo DNI
            };

            // Act
            var result = _repository.Create(v4);

            // Assert
            result.IsFailure.Should().BeTrue(); // Aquí sí será True porque llegamos al límite
            // Nota: Asegúrate de que el tipo de error coincida con el que lanzas en el repositorio
            result.Error.Should().BeOfType<VehiculoError.Validation>(); 
        }

        [Test]
        public void GetById_CuandoNoExiste_RetornarNull() {
            // Act
            var result = _repository.GetById(9999999);
            
            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void GetByMatricula_CuandoNoExiste_RetornarFailure() {
            // Act
            var result = _repository.GetByMatricula("12345-BBC");
            
            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void GetByDniPropietario_RetornarNull() {
            // Act 
            var result = _repository.GetByDniPropietario("33333333333333333A");
            
            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void ExistMatricula_CuandoNoExiste_RetonarFalse() {
            // Act
            var result = _repository.ExistsMatricula("12345-BBB");
            
            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void ExistDniPropietario_RetornarFalse() {
            // Act
            var result = _repository.ExistsDniPropietario("123456787654A");
            
            // Assert
            result.Should().BeFalse();
        }
        
        [Test]
        public void Update_CuandoNoExiste_RetornarFailure() {
            // Arrange
            var v = new Vehiculo { Matricula = "1111-AAA", DniPropietario = "34567890V", Marca = "A", Modelo = "A" };
            
            // Act
            var result = _repository.Update(12323453, v);
            
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().BeOfType<VehiculoError.NotFound>();
            (result.Error as VehiculoError.NotFound)?.Id.Should().Be("12323453");
        }

        [Test]
        public void Update_ConMatriculaEnOtro_RetornarFailure() {
            var v1 = new Vehiculo {
                 Matricula = "5678-DFH", Marca = "Volkswagen", Modelo = "Golf", Cilindrada = 2000,
                Motor = Motor.Diesel, DniPropietario = "23456789D"
            };
            var v2 = new Vehiculo {
                Matricula = "1234-DFC", Marca = "Tesla", Modelo = "Model 3", Cilindrada = 0,
                Motor = Motor.Electrico, DniPropietario = "34567890V"
            };

            _repository.Create(v1);
            _repository.Create(v2);
            
            // Act
            var result = _repository.Update(2, v1);
            
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().BeOfType<VehiculoError.MatriculaAlreadyExists>();
            (result.Error as VehiculoError.MatriculaAlreadyExists)?.Matricula.Should().Be("5678-DFH");
        }

        [Test]
        public void Update_CuandoNuevoDueñoYaTieneLimiteDe3_RetornarFailure() {
            // Arrange
            var dniSaturado = "23456789D";
            var dniLibre = "99999999Z";

            // 1. Creamos 3 coches para el primer dueño (llegamos al límite)
            for (int i = 0; i < 3; i++) {
                _repository.Create(new Vehiculo { 
                    Matricula = $"000{i}-AAA", DniPropietario = dniSaturado, Marca="A", Modelo="A" 
                });
            }

            // 2. Creamos un coche para otro dueño
            var vExtra = _repository.Create(new Vehiculo { 
                Matricula = "7777-BBB", DniPropietario = dniLibre, Marca="B", Modelo="B" 
            }).Value;

            // Act: Intentamos pasar el coche del dueño libre al dueño que ya tiene 3
            var a = vExtra with { DniPropietario = dniSaturado };
            var result = _repository.Update(vExtra.Id, a);

            // Assert
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().BeOfType<VehiculoError.Validation>();
            // Cambiamos "Límite alcanzado" por "límite de 3" que es lo que devuelve el repo
            result.Error.ToString().Should().Contain("Límite de 3");
        }

        [Test]
        public void Delete_CuandoNoExiste_DeberiaRetornarNull() {
            // Act
            var result = _repository.Delete(123);
            
            // Assert
            result.Should().BeNull();
        }
        
        [Test]
        public void Restore_CuandoNoExiste_DeberiaRetornarFailure() {
            // Act
            var resultado = _repository.Restore(999);

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<VehiculoError.NotFound>();
        }
    }
    
    [TestFixture]
    public class CasosMixtos {
        [SetUp]
        public void SetUp() {
            _tempFile = Path.GetTempFileName();
            _repository = new VehiculoJsonRepository(_tempFile);
            _repository.DeleteAll();
        }
        
        
        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }
        
        private string _tempFile = null!;
        private VehiculoJsonRepository _repository = null!;
        [Test]
        public void Restore_CuandoElimanadoLogicamente_Restaurar() {
            var v1 = new Vehiculo {
                Matricula = "5678-DFH", Marca = "Volkswagen", Modelo = "Golf", Cilindrada = 2000,
                Motor = Motor.Diesel, DniPropietario = "23456789D"
            };
            var c = _repository.Create(v1).Value;
            _repository.Delete(c.Id);
            
            // Act
            var result = _repository.Restore(c.Id);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            var restaurado = result.Value;
            restaurado.IsDeleted.Should().BeFalse(); // Usa la variable 'restaurado'
            restaurado.DeletedAt.Should().BeNull();
            _repository.GetById(c.Id).Should().NotBeNull();
        }

        [Test]
        public void CountVehiculos_SinEliminados_ContarSoloActivos() {
            // Arrange
            _repository.Create(new Vehiculo {
                Matricula = "5678-DFH", Marca = "Volkswagen", Modelo = "Golf", Cilindrada = 2000,
                Motor = Motor.Diesel, DniPropietario = "23456789D"
            });
            var v2 = _repository.Create(new Vehiculo {
                Matricula = "1234-DFC", Marca = "Tesla", Modelo = "Model 3", Cilindrada = 0,
                Motor = Motor.Electrico, DniPropietario = "34567890V"
            }).Value;
            _repository.Create(new Vehiculo {
                Matricula = "1234_VVV", Marca = "Volkswagen", Modelo = "Golf", Cilindrada = 2000,
                Motor = Motor.Diesel, DniPropietario = "23456789D"
            });
            _repository.Delete(v2.Id);
            
            // Act
            var result = _repository.CountVehiculos();
            
            // Assert
            result.Should().Be(2);
            
        }

        [Test]
        public void CountVehiculos_IncluyendoEliminados_ContarSoloActivos() {
            // Arrange
            _repository.Create(new Vehiculo {
                Matricula = "5678-DFH", Marca = "Volkswagen", Modelo = "Golf", Cilindrada = 2000,
                Motor = Motor.Diesel, DniPropietario = "23456789D"
            });
            var v2 = _repository.Create(new Vehiculo {
                Matricula = "1234-DFC", Marca = "Tesla", Modelo = "Model 3", Cilindrada = 0,
                Motor = Motor.Electrico, DniPropietario = "34567890V"
            }).Value;
            _repository.Delete(v2.Id);
            
            // Act
            var result = _repository.CountVehiculos();
            
            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void DeletedAll_VaciarRepositorio() {
            // Arrange
            _repository.Create(new Vehiculo {
                Matricula = "5678-DFH", Marca = "Volkswagen", Modelo = "Golf", Cilindrada = 2000,
                Motor = Motor.Diesel, DniPropietario = "23456789D"
            });
            _repository.Create(new Vehiculo {
                Matricula = "1234-DFC", Marca = "Tesla", Modelo = "Model 3", Cilindrada = 0,
                Motor = Motor.Electrico, DniPropietario = "34567890V"
            });
            
            // Act
            var resultado = _repository.DeleteAll();
            
            // Assert
            resultado.Should().BeTrue();
            _repository.GetAll().Should().BeEmpty();
        }
    }
}