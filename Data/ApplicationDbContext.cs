using Microsoft.EntityFrameworkCore;
using CarSlineAPI.Models.Entities;

namespace CarSlineAPI.Data
{
    /// <summary>
    /// Contexto de base de datos para CarSline
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets - Tablas de la base de datos
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Vehiculo> Vehiculos { get; set; }
        public DbSet<TipoServicio> TiposServicio { get; set; }
        public DbSet<ServicioExtra> ServiciosExtra { get; set; }
        public DbSet<ServicioFrecuente> ServiciosFrecuentes { get; set; }
        public DbSet<Orden> Ordenes { get; set; }
        public DbSet<OrdenGeneral> OrdenesGenerales { get; set; }
        public DbSet<OrdenServicioExtra> OrdenesServiciosExtra { get; set; }
        public DbSet<HistorialServicio> HistorialServicios { get; set; }
        public DbSet<Refaccion> Refacciones { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indices/constraints que coincidan con tu BD existente (ajusta si el nombre de la columna es distinto)
            modelBuilder.Entity<Cliente>().HasIndex(c => c.TelefonoMovil).IsUnique(false);
            modelBuilder.Entity<Vehiculo>().HasIndex(v => v.VIN).IsUnique();
            modelBuilder.Entity<Rol>().HasIndex(r => r.NombreRol).IsUnique();

            // Relaciones
            modelBuilder.Entity<Vehiculo>()
                .HasOne(v => v.Cliente)
                .WithMany()
                .HasForeignKey(v => v.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrdenServicioExtra>()
                .HasOne(o => o.Orden)
                .WithMany(o => o.ServiciosExtra)
                .HasForeignKey(o => o.OrdenId);

            // Ajustes de nombres si tu BD usa columnas con otros nombres: usar .HasColumnName("...") aquí.

            // ============================================
            // CONFIGURACIÓN DE USUARIO
            // ============================================

            modelBuilder.Entity<Usuario>(entity =>
            {
                // Índice único en NombreUsuario
                entity.HasIndex(e => e.NombreUsuario)
                    .IsUnique()
                    .HasDatabaseName("idx_nombre_usuario");

                // Índice en RolId
                entity.HasIndex(e => e.RolId)
                    .HasDatabaseName("idx_rol");

                // Índice en Activo
                entity.HasIndex(e => e.Activo)
                    .HasDatabaseName("idx_activo");

                // Valor por defecto para FechaCreacion
                entity.Property(e => e.FechaCreacion)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Valor por defecto para Activo
                entity.Property(e => e.Activo)
                    .HasDefaultValue(true);

                // Relación con Rol (muchos usuarios tienen un rol)
                entity.HasOne(e => e.Rol)
                    .WithMany(r => r.Usuarios)
                    .HasForeignKey(e => e.RolId)
                    .OnDelete(DeleteBehavior.Restrict); // No eliminar rol si tiene usuarios

                // Auto-referencia para CreadoPor (un usuario puede ser creado por otro)
                entity.HasOne(e => e.CreadoPor)
                    .WithMany()
                    .HasForeignKey(e => e.CreadoPorId)
                    .OnDelete(DeleteBehavior.SetNull); // Si se elimina el creador, se pone NULL
            });

            // ============================================
            // CONFIGURACIÓN DE ROL
            // ============================================
            modelBuilder.Entity<Rol>(entity =>
            {
                // Índice único en NombreRol
                entity.HasIndex(e => e.NombreRol)
                    .IsUnique()
                    .HasDatabaseName("idx_nombre_rol");

                // Valor por defecto para FechaCreacion
                entity.Property(e => e.FechaCreacion)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
     }
}