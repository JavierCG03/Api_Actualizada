using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarSlineAPI.Models.Entities
{
    // ============================================
    // ENTIDADES DE BASE DE DATOS
    // ============================================

    [Table("roles")]
    public class Rol
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string NombreRol { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Descripcion { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // Relación con usuarios
        public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }


    [Table("usuarios")]
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public int RolId { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? UltimoAcceso { get; set; }

        public bool Activo { get; set; } = true;

        public int? CreadoPorId { get; set; }

        // Navegación
        [ForeignKey("RolId")]
        public virtual Rol? Rol { get; set; }

        [ForeignKey("CreadoPorId")]
        public virtual Usuario? CreadoPor { get; set; }
    }

    [Table("Clientes")]
    public class Cliente
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(250)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string RFC { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string TelefonoMovil { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? TelefonoCasa { get; set; }

        [MaxLength(150)]
        public string? CorreoElectronico { get; set; }

        [MaxLength(150)]
        public string? Colonia { get; set; }

        [MaxLength(150)]
        public string? Calle { get; set; }

        [MaxLength(50)]
        public string? NumeroExterior { get; set; }

        [MaxLength(150)]
        public string? Municipio { get; set; }

        [MaxLength(150)]
        public string? Estado { get; set; }

        [MaxLength(100)]
        public string? Pais { get; set; }

        [MaxLength(20)]
        public string? CodigoPostal { get; set; }

        public bool Activo { get; set; } = true;
    }

    [Table("Vehiculos")]
    public class Vehiculo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClienteId { get; set; }

        [Required, MaxLength(50)]
        public string VIN { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Marca { get; set; }
        [MaxLength(100)]
        public string? Version { get; set; }

        [MaxLength(100)]
        public string? Modelo { get; set; }

        public int? Anio { get; set; }

        [MaxLength(50)]
        public string? Color { get; set; }

        [MaxLength(20)]
        public string? Placas { get; set; }

        public int KilometrajeInicial { get; set; }

        public bool Activo { get; set; } = true;

        // navegación
        [ForeignKey("ClienteId")]
        public Cliente? Cliente { get; set; }
    }


    [Table("TiposServicio")]
    public class TipoServicio
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string NombreServicio { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Descripcion { get; set; }

        public decimal PrecioBase { get; set; }

        public bool Activo { get; set; } = true;
    }


    [Table("ServiciosFrecuentes")]
    public class ServicioFrecuente
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string NombreServicio { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Descripcion { get; set; }

        public decimal Precio { get; set; }

        [MaxLength(100)]
        public string? Categoria { get; set; }

        public bool Activo { get; set; } = true;
    }

    [Table("OrdenesGenerales")]
    public class OrdenGeneral
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string NumeroOrden { get; set; } = string.Empty;
        public int TipoOrdenId { get; set; } // 1=SRV, etc.
        public int ClienteId { get; set; }
        public int VehiculoId { get; set; }
        public int AsesorId { get; set; }
        public int? TipoServicioId { get; set; }
        public int KilometrajeActual { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime FechaHoraPromesaEntrega { get; set; }
        public DateTime? FechaInicioProceso { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public string? ObservacionesAsesor { get; set; }
        public string? ObservacionesJefe { get; set; }
        public decimal CostoTotal { get; set; }
        public decimal TiempoTotalHoras { get; set; }
        public int TotalTrabajos { get; set; }
        public int TrabajosCompletados { get; set; }
        public decimal ProgresoGeneral { get; set; }
        public int EstadoOrdenId { get; set; } = 1;
        public bool Activo { get; set; } = true;
        public bool TieneEvidencia { get; set; } = false;

        // relaciones
        public virtual ICollection<TrabajoPorOrden> Trabajos { get; set; } = new List<TrabajoPorOrden>();
        public virtual Cliente Cliente { get; set; }
        public virtual Vehiculo Vehiculo { get; set; }
        public virtual Usuario Asesor { get; set; }
        public virtual TipoServicio TipoServicio { get; set; }

    }


    [Table("HistorialServicios")]
    public class HistorialServicio
    {
        [Key]
        public int Id { get; set; }

        public int VehiculoId { get; set; }

        public int OrdenId { get; set; }

        public int TipoServicioId { get; set; }

        public int KilometrajeRegistrado { get; set; }

        public DateTime FechaServicio { get; set; }

        public int? ProximoServicioKm { get; set; }

        public DateTime? ProximoServicioFecha { get; set; }

        public decimal CostoTotal { get; set; }
    }

    [Table("refacciones")]
    public class Refaccion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string NumeroParte { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TipoRefaccion { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Ubicacion { get; set; }

        [MaxLength(50)]
        public string? MarcaVehiculo { get; set; }

        [MaxLength(50)]
        public string? Modelo { get; set; }

        public int? Anio { get; set; }

        [Required]
        public int Cantidad { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public DateTime FechaUltimaModificacion { get; set; } = DateTime.Now;

        public bool Activo { get; set; } = true;
    }

    [Table("trabajopororden")]
    public class TrabajoPorOrden
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrdenGeneralId { get; set; }

        [Required]
        [Column(TypeName = "TEXT")]
        public string Trabajo { get; set; } = string.Empty;

        public int? TecnicoAsignadoId { get; set; }

        public DateTime? FechaHoraAsignacionTecnico { get; set; }

        public DateTime? FechaHoraInicio { get; set; }

        public DateTime? FechaHoraTermino { get; set; }

        [Column(TypeName = "TEXT")]
        public string? IndicacionesTrabajo { get; set; }

        [Column(TypeName = "TEXT")]
        public string? ComentariosTecnico { get; set; }

        [Column(TypeName = "TEXT")]
        public string? ComentariosJefeTaller { get; set; }

        [Required]
        public int EstadoTrabajo { get; set; } = 1; // 1=Pendiente

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // Navegación
        [ForeignKey("OrdenGeneralId")]
        public virtual OrdenGeneral? OrdenGeneral { get; set; }

        [ForeignKey("TecnicoAsignadoId")]
        public virtual Usuario? TecnicoAsignado { get; set; }

        [ForeignKey("EstadoTrabajo")]
        public virtual EstadoTrabajo? EstadoTrabajoNavegacion { get; set; }
    }

    [Table("estadostrabajo")]
    public class EstadoTrabajo
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string NombreEstado { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Descripcion { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }

        public int? Orden { get; set; }
    }

    [Table("pausastrabajos")]
    public class pausatrabajo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TrabajoId { get; set; }

        [Required]
        public int OrdenGeneralId { get; set; }
        public DateTime? FechaHoraPausa { get; set; }

        public DateTime? FechaHoraReanudacion { get; set; }

        [Required]
        [Column(TypeName = "TEXT")]
        public string Motivo { get; set; } = string.Empty;
       
        // Navegación
        [ForeignKey("TrabajoId")]
        public virtual TrabajoPorOrden? TrabajoPorOrden { get; set; }

        [ForeignKey("OrdenGeneralId")]
        public virtual OrdenGeneral? OrdenGeneral { get; set; }
    }

    [Table("checklistservicios")]
    
    public class CheckListServicio
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TrabajoId { get; set; }

        [Required]
        public int OrdenGeneralId { get; set; }

        [Required]
        [Column(TypeName = "TEXT")]
        public string Trabajo { get; set; } = string.Empty;

        // Sistema de Dirección
        [Required]
        [MaxLength(15)]
        public string Bieletas { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string Terminales { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string CajaDireccion { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string Volante { get; set; } = string.Empty;

        // Sistema de Suspensión
        [Required]
        [MaxLength(15)]
        public string AmortiguadoresDelanteros { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string AmortiguadoresTraseros { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string BarraEstabilizadora { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string Horquillas { get; set; } = string.Empty;

        // Neumáticos
        [Required]
        [MaxLength(15)]
        public string NeumaticosDelanteros { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string NeumaticosTraseros { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string Balanceo { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string Alineacion { get; set; } = string.Empty;

        // Luces
        [Required]
        [MaxLength(15)]
        public string LucesAltas { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string LucesBajas { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string LucesAntiniebla { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string LucesReversa { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string LucesDireccionales { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string LucesIntermitentes { get; set; } = string.Empty;

        // Sistema de Frenos
        [Required]
        [MaxLength(15)]
        public string DiscosTamboresDelanteros { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string DiscosTamboresTraseros { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string BalatasDelanteras { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string BalatasTraseras { get; set; } = string.Empty;

        // Piezas Reemplazadas
        [Required]
        public bool ReemplazoAceiteMotor { get; set; }

        [Required]
        public bool ReemplazoFiltroAceite { get; set; }

        [Required]
        public bool ReemplazoFiltroAireMotor { get; set; }

        [Required]
        public bool ReemplazoFiltroAirePolen { get; set; }

        // Revisión de Niveles
        [Required]
        public bool NivelLiquidoFrenos { get; set; } = true;

        [Required]
        public bool NivelAnticongelante { get; set; } = true;

        [Required]
        public bool NivelDepositoLimpiaparabrisas { get; set; } = true;

        [Required]
        public bool NivelAceiteMotor { get; set; } = true;

        // Trabajos Realizados
        [Required]
        public bool DescristalizacionTamboresDiscos { get; set; } = true;

        [Required]
        public bool AjusteFrenos { get; set; } = true;

        [Required]
        public bool CalibracionPresionNeumaticos { get; set; } = true;

        [Required]
        public bool TorqueNeumaticos { get; set; } = true;
        public bool RotacionNeumaticos { get; set; } = false;

        // Navegación
        [ForeignKey("TrabajoId")]
        public virtual TrabajoPorOrden? TrabajoPorOrden { get; set; }

        [ForeignKey("OrdenGeneralId")]
        public virtual OrdenGeneral? OrdenGeneral { get; set; }
    }

    [Table("evidenciasorden")]
    public class Evidenciaorden
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrdenGeneralId { get; set; }
        public DateTime? FechaRegistro { get; set; }

        [Required]
        [MaxLength(500)]
        public string RutaImagen { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Descripcion { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;

        [ForeignKey("OrdenGeneralId")]
        public virtual OrdenGeneral? OrdenGeneral { get; set; }
    }
}


