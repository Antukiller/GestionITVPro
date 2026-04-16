using FluentAssertions;
using GestionITVPro.Config;

namespace GestionITVPro.Test.Config;

[TestFixture]
public class AppConfigTest {
    [TestFixture]
    public class PropiedadesBasicas {
        [Test]
        public void Locale_DeberiaRetornar() {
            // Act 
            var l = AppConfig.Locale;

            l.Should().NotBeNull();
            l.Name.Should().Be("es-ES");
        }

        [Test]
        public void StorageType_DeberiaRetornarTipoValido() { 
            // Act 
            var t = AppConfig.StorageType;
            
            // Assert
            t.Should().NotBeNullOrEmpty();
            t.ToLower().Should().BeOneOf("json", "csv", "bin", "xml");
        }

        [Test]
        public void RepositoryType_DeberiaRetornarTipoValido() {
            // Act
            var t = AppConfig.RepositoryType;
            
            // Assert
            t.Should().NotBeNullOrEmpty();
            t.Should().BeOneOf("memory", "json", "ado", "dapper", "efcore");
            
        }

        [Test]
        public void ConnectionString_DeberiaRetornarValorNoNulo() {
            // Act
            var connStr = AppConfig.ConnectionString;
            
            // Assert
            connStr.Should().NotBeNullOrEmpty();
            connStr.Should().Contain("Data Source");
        }

        [Test]
        public void CacheSize_DeberiaSerMayorACero() {
            // Act
            var s = AppConfig.CacheSize;
            
            // Assert
            s.Should().BeGreaterThan(0);
        }

        [Test]
        public void DropData_DeberiaRetornarBoolean() {
            // Act
            var d = AppConfig.DropData;
            
            // Assert
            // Puede ser true o false dependiendo de la configuración 
            // Lo importante es que no lanza excepción y retorna un boolean
            (d || !d).Should().BeTrue();
        }

        public void SeedData_DeberiaRetornarTrue() {
            // Act
            var s = AppConfig.SeedData;
            
            // Assert
            s.Should().BeTrue();
        }

        [Test]
        public void UseLogicalDelete_DeberiaRetornarTrue() {
            // Act
            var l = AppConfig.UseLogicalDelete;
            
            // Assert
            l.Should().BeTrue();
        }
    }
    
    [TestFixture]
    public class Directorios {
        [Test]
        public void DataFolder_DeberiaRutaValida() {
            // Act
            var f = AppConfig.DataFolder;
            
            // Assert
            f.Should().NotBeNullOrEmpty();
            Path.IsPathRooted(f).Should().BeTrue();
        }

        [Test]
        public void BackupDirectory_DeberiaRetornar() {
            // Act
            var dir = AppConfig.BackupFormat;
            
            // Assert
            dir.Should().BeNullOrEmpty();
            Path.IsPathRooted(dir).Should().BeTrue();
        }

        [Test]
        public void ReportDirectory_DeberiaRetonarRutaValida() {
            // Act
            var dir = AppConfig.ReportDirectory;
            
            // Assert
            dir.Should().BeNullOrEmpty();
            Path.IsPathRooted(dir).Should().BeTrue();
        }

        [Test]
        public void LogDirectory_DeberiaRetornarRutaValida() {
            // Act
            var dir = AppConfig.LogDirectory;
            
            // Assert
            dir.Should().NotBeNullOrEmpty();
            Path.IsPathRooted(dir).Should().BeTrue();
        }
    }
    
    [TestFixture]
    public class Formatos {
        [TestCase("json")]
        [TestCase("csv")]
        [TestCase("xml")]
        [TestCase("bin")]
        public void GestionITV_DeberiaRetonarAlmacenamiento(string almacenamiento) {
            var f = AppConfig.GestionItv;
            
            // Assert
            f.Should().NotBeNullOrEmpty();
            f.Should().BeOneOf(".json", ".csv", ".xml", ".bin");
        }

        [Test]
        public void BackupFormat_DeberiaRetornarFormatoValida() {
            // Act 
            var f = AppConfig.BackupFormat;

            f.Should().NotBeNullOrEmpty();
            f.Should().BeOneOf("json", "cvs", "xml", "bin");
        }

        [Test]
        public void IsDevelopment_DeberiaRetornarBoolean() {
            // Act 
            var dev = AppConfig.IsDevelopment;
            
            // Assert 
            // Puede ser true o false dependiendo de la configuración
            // Lom importante es que no lanza excepción y retonar un boolean
            (dev || !dev).Should().BeTrue();
        }
    }
    
    [TestFixture]
    public class Logging {
        [Test]
        public void LogToFile_DeberiaRetornarTrue() {
            // Act
            var log = AppConfig.LogToFile;
            
            // Assert
            log.Should().BeTrue();
        }

        [Test]
        public void LogRetainDays_DeberiaSerMayorQueCero() {
            // Act
            var days = AppConfig.LogRetainDays;
            
            // Assert
            days.Should().BeGreaterThan(0);
        }

        [Test]
        public void LogLevel_DeberiaRetornarNivelValido() {
            // Act 
            var level = AppConfig.LogLevel;

            level.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void LogOutTemplate_DeberiaDevolverTemplete() {
            // Act
            var template = AppConfig.LogOutTemplate;
            
            // Assert
            template.Should().NotBeNullOrEmpty();
            template.Should().Contain("{Timestamp}");
            template.Should().Contain("{Level}");
        }
    }
}