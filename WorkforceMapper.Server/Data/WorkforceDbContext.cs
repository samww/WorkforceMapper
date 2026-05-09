using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Data;

public class WorkforceDbContext(DbContextOptions<WorkforceDbContext> options) : DbContext(options)
{
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductApplication> ProductApplications => Set<ProductApplication>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectProduct> ProjectProducts => Set<ProjectProduct>();
    public DbSet<Mapping> Mappings => Set<Mapping>();
    public DbSet<ApplicationDependency> ApplicationDependencies => Set<ApplicationDependency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mapping>().HasKey(m => new { m.ResourceId, m.ActivityId, m.Month });
        modelBuilder.Entity<ProductApplication>().HasKey(pa => new { pa.ProductId, pa.ApplicationId });
        modelBuilder.Entity<ProjectProduct>().HasKey(pp => new { pp.ProjectId, pp.ProductId });
        modelBuilder.Entity<ApplicationDependency>().HasKey(d => new { d.ApplicationId, d.DependsOnApplicationId });
        modelBuilder.Entity<ApplicationDependency>()
            .HasOne(d => d.Application).WithMany(a => a.Dependencies)
            .HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ApplicationDependency>()
            .HasOne(d => d.DependsOnApplication).WithMany()
            .HasForeignKey(d => d.DependsOnApplicationId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Resource>().HasIndex(r => r.ResourceId).IsUnique();

        modelBuilder.Entity<Mapping>()
            .HasOne(m => m.Resource).WithMany(r => r.Mappings).HasForeignKey(m => m.ResourceId);
        modelBuilder.Entity<Mapping>()
            .HasOne(m => m.Activity).WithMany(a => a.Mappings).HasForeignKey(m => m.ActivityId);

        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Application).WithMany(app => app.Activities)
            .HasForeignKey(a => a.ApplicationId).IsRequired(false);
        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Project).WithMany(p => p.Activities)
            .HasForeignKey(a => a.ProjectId).IsRequired(false);

        modelBuilder.Entity<ProductApplication>()
            .HasOne(pa => pa.Product).WithMany(p => p.ProductApplications).HasForeignKey(pa => pa.ProductId);
        modelBuilder.Entity<ProductApplication>()
            .HasOne(pa => pa.Application).WithMany(a => a.ProductApplications).HasForeignKey(pa => pa.ApplicationId);

        modelBuilder.Entity<ProjectProduct>()
            .HasOne(pp => pp.Project).WithMany(p => p.ProjectProducts).HasForeignKey(pp => pp.ProjectId);
        modelBuilder.Entity<ProjectProduct>()
            .HasOne(pp => pp.Product).WithMany(p => p.ProjectProducts).HasForeignKey(pp => pp.ProductId);

        SeedApplications(modelBuilder);
        SeedApplicationDependencies(modelBuilder);
        SeedProducts(modelBuilder);
        SeedProductApplications(modelBuilder);
        SeedProjects(modelBuilder);
        SeedProjectProducts(modelBuilder);
        SeedActivities(modelBuilder);
    }

    private static void SeedApplications(ModelBuilder m) =>
        m.Entity<Application>().HasData(
            new { Id = 1,  Name = "CoreDataWarehouse",  MinSupportFte = 2.0, RecommendedSupportFte = 3.0 },
            new { Id = 2,  Name = "APIGateway",         MinSupportFte = 1.5, RecommendedSupportFte = 2.5 },
            new { Id = 3,  Name = "SurveyEngine",       MinSupportFte = 2.0, RecommendedSupportFte = 3.0 },
            new { Id = 4,  Name = "AuthService",        MinSupportFte = 4.0, RecommendedSupportFte = 5.0 },
            new { Id = 5,  Name = "ObservabilityStack", MinSupportFte = 1.0, RecommendedSupportFte = 2.0 },
            new { Id = 6,  Name = "InsightHub",         MinSupportFte = 1.5, RecommendedSupportFte = 2.5 },
            new { Id = 7,  Name = "DataMapper",         MinSupportFte = 1.5, RecommendedSupportFte = 2.5 },
            new { Id = 8,  Name = "QuotaManager",       MinSupportFte = 1.0, RecommendedSupportFte = 1.5 },
            new { Id = 9,  Name = "SegmentBuilder",     MinSupportFte = 1.0, RecommendedSupportFte = 2.0 },
            new { Id = 10, Name = "FieldOps",           MinSupportFte = 2.0, RecommendedSupportFte = 3.0 },
            new { Id = 11, Name = "WebCollect",         MinSupportFte = 1.5, RecommendedSupportFte = 2.5 },
            new { Id = 12, Name = "SampleRouter",       MinSupportFte = 1.0, RecommendedSupportFte = 1.5 },
            new { Id = 13, Name = "CallCentre Suite",   MinSupportFte = 1.0, RecommendedSupportFte = 1.5 },
            new { Id = 14, Name = "DiaryTracker",       MinSupportFte = 1.0, RecommendedSupportFte = 2.0 },
            new { Id = 15, Name = "ClientPortal",       MinSupportFte = 1.5, RecommendedSupportFte = 2.0 },
            new { Id = 16, Name = "ReportFactory",      MinSupportFte = 1.0, RecommendedSupportFte = 1.5 },
            new { Id = 17, Name = "DeliveryTracker",    MinSupportFte = 1.0, RecommendedSupportFte = 1.5 },
            new { Id = 18, Name = "PanelIQ",            MinSupportFte = 2.0, RecommendedSupportFte = 3.0 }
        );

    private static void SeedApplicationDependencies(ModelBuilder m) =>
        m.Entity<ApplicationDependency>().HasData(
            // CoreDataWarehouse and APIGateway depend on AuthService
            new { ApplicationId = 1,  DependsOnApplicationId = 4 },
            new { ApplicationId = 2,  DependsOnApplicationId = 4 },
            // Analytics apps depend on CoreDataWarehouse (which depends on AuthService)
            new { ApplicationId = 3,  DependsOnApplicationId = 1 },
            new { ApplicationId = 6,  DependsOnApplicationId = 1 },
            new { ApplicationId = 9,  DependsOnApplicationId = 1 },
            // Remaining apps depend directly on AuthService
            new { ApplicationId = 5,  DependsOnApplicationId = 4 },
            new { ApplicationId = 7,  DependsOnApplicationId = 4 },
            new { ApplicationId = 8,  DependsOnApplicationId = 4 },
            new { ApplicationId = 10, DependsOnApplicationId = 4 },
            new { ApplicationId = 11, DependsOnApplicationId = 4 },
            new { ApplicationId = 12, DependsOnApplicationId = 4 },
            new { ApplicationId = 13, DependsOnApplicationId = 4 },
            new { ApplicationId = 14, DependsOnApplicationId = 4 },
            new { ApplicationId = 15, DependsOnApplicationId = 4 },
            new { ApplicationId = 16, DependsOnApplicationId = 4 },
            new { ApplicationId = 17, DependsOnApplicationId = 4 },
            new { ApplicationId = 18, DependsOnApplicationId = 4 }
        );

    private static void SeedProducts(ModelBuilder m) =>
        m.Entity<Product>().HasData(
            new { Id = 1, Name = "DataLicensing",              ArrUsd = 6200000m },
            new { Id = 2, Name = "MediaMeasurement",           ArrUsd = 5100000m },
            new { Id = 3, Name = "RetailInsights",             ArrUsd = 4600000m },
            new { Id = 4, Name = "BrandTracking Platform",     ArrUsd = 4200000m },
            new { Id = 5, Name = "CustomerSatisfaction Suite", ArrUsd = 3800000m },
            new { Id = 6, Name = "ConsumerPanel",              ArrUsd = 3400000m },
            new { Id = 7, Name = "FieldworkServices",          ArrUsd = 1600000m }
        );

    private static void SeedProductApplications(ModelBuilder m) =>
        m.Entity<ProductApplication>().HasData(
            new { ProductId = 1, ApplicationId = 1  },
            new { ProductId = 1, ApplicationId = 2  },
            new { ProductId = 1, ApplicationId = 15 },
            new { ProductId = 2, ApplicationId = 14 },
            new { ProductId = 2, ApplicationId = 7  },
            new { ProductId = 2, ApplicationId = 6  },
            new { ProductId = 2, ApplicationId = 1  },
            new { ProductId = 3, ApplicationId = 3  },
            new { ProductId = 3, ApplicationId = 9  },
            new { ProductId = 3, ApplicationId = 6  },
            new { ProductId = 3, ApplicationId = 1  },
            new { ProductId = 4, ApplicationId = 3  },
            new { ProductId = 4, ApplicationId = 18 },
            new { ProductId = 4, ApplicationId = 6  },
            new { ProductId = 4, ApplicationId = 1  },
            new { ProductId = 5, ApplicationId = 3  },
            new { ProductId = 5, ApplicationId = 11 },
            new { ProductId = 5, ApplicationId = 16 },
            new { ProductId = 5, ApplicationId = 1  },
            new { ProductId = 6, ApplicationId = 18 },
            new { ProductId = 6, ApplicationId = 11 },
            new { ProductId = 6, ApplicationId = 7  },
            new { ProductId = 6, ApplicationId = 12 },
            new { ProductId = 7, ApplicationId = 10 },
            new { ProductId = 7, ApplicationId = 8  },
            new { ProductId = 7, ApplicationId = 12 },
            new { ProductId = 7, ApplicationId = 13 }
        );

    private static void SeedProjects(ModelBuilder m) =>
        m.Entity<Project>().HasData(
            new { Id = 1,  Name = "P01 CoreDataWarehouse Cloud Migration" },
            new { Id = 2,  Name = "P02 AuthService Replacement" },
            new { Id = 3,  Name = "P03 Security & Compliance Programme" },
            new { Id = 4,  Name = "P04 DevPortal Rebuild" },
            new { Id = 5,  Name = "P05 ML Forecasting Platform" },
            new { Id = 6,  Name = "P06 Real-Time Analytics Initiative" },
            new { Id = 7,  Name = "P07 SegmentBuilder Enhancement" },
            new { Id = 8,  Name = "P08 CallCentre Suite Replacement" },
            new { Id = 9,  Name = "P09 DiaryTracker Mobile Rewrite" },
            new { Id = 10, Name = "P10 Panel Automation Programme" },
            new { Id = 11, Name = "P11 Client Onboarding Automation" },
            new { Id = 12, Name = "P12 API Delivery Programme" }
        );

    private static void SeedProjectProducts(ModelBuilder m) =>
        m.Entity<ProjectProduct>().HasData(
            new { ProjectId = 1,  ProductId = 1 },
            new { ProjectId = 1,  ProductId = 2 },
            new { ProjectId = 1,  ProductId = 3 },
            new { ProjectId = 1,  ProductId = 4 },
            new { ProjectId = 1,  ProductId = 5 },
            new { ProjectId = 2,  ProductId = 1 },
            new { ProjectId = 2,  ProductId = 2 },
            new { ProjectId = 2,  ProductId = 3 },
            new { ProjectId = 2,  ProductId = 4 },
            new { ProjectId = 2,  ProductId = 5 },
            new { ProjectId = 2,  ProductId = 6 },
            new { ProjectId = 2,  ProductId = 7 },
            new { ProjectId = 3,  ProductId = 1 },
            new { ProjectId = 3,  ProductId = 2 },
            new { ProjectId = 3,  ProductId = 3 },
            new { ProjectId = 3,  ProductId = 4 },
            new { ProjectId = 3,  ProductId = 5 },
            new { ProjectId = 3,  ProductId = 6 },
            new { ProjectId = 3,  ProductId = 7 },
            new { ProjectId = 5,  ProductId = 2 },
            new { ProjectId = 5,  ProductId = 3 },
            new { ProjectId = 5,  ProductId = 4 },
            new { ProjectId = 6,  ProductId = 1 },
            new { ProjectId = 6,  ProductId = 2 },
            new { ProjectId = 7,  ProductId = 3 },
            new { ProjectId = 8,  ProductId = 7 },
            new { ProjectId = 9,  ProductId = 2 },
            new { ProjectId = 10, ProductId = 4 },
            new { ProjectId = 10, ProductId = 6 },
            new { ProjectId = 11, ProductId = 5 },
            new { ProjectId = 11, ProductId = 1 },
            new { ProjectId = 12, ProductId = 1 }
        );

    private static void SeedActivities(ModelBuilder m) =>
        m.Entity<Activity>().HasData(
            new { Id = 1,  Name = "CoreDataWarehouse - Support",                    ActivityType = "Support",        ApplicationId = (int?)1,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 2,  Name = "CoreDataWarehouse - Minor Changes",              ActivityType = "Minor Changes",  ApplicationId = (int?)1,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 3,  Name = "APIGateway - Support",                           ActivityType = "Support",        ApplicationId = (int?)2,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 4,  Name = "SurveyEngine - Support",                         ActivityType = "Support",        ApplicationId = (int?)3,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 5,  Name = "AuthService - Support",                          ActivityType = "Support",        ApplicationId = (int?)4,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 6,  Name = "ObservabilityStack - Support",                   ActivityType = "Support",        ApplicationId = (int?)5,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 7,  Name = "P01 CoreDataWarehouse Cloud Migration - Project", ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)1,  Division = "Platform Engineering" },
            new { Id = 8,  Name = "P02 AuthService Replacement - Project",          ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)2,  Division = "Platform Engineering" },
            new { Id = 9,  Name = "P03 Security & Compliance Programme - Project",  ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)3,  Division = "Platform Engineering" },
            new { Id = 10, Name = "P04 DevPortal Rebuild - Project",                ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)4,  Division = "Platform Engineering" },
            new { Id = 11, Name = "InsightHub - Support",                           ActivityType = "Support",        ApplicationId = (int?)6,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 12, Name = "InsightHub - Minor Changes",                     ActivityType = "Minor Changes",  ApplicationId = (int?)6,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 13, Name = "DataMapper - Support",                           ActivityType = "Support",        ApplicationId = (int?)7,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 14, Name = "QuotaManager - Support",                         ActivityType = "Support",        ApplicationId = (int?)8,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 15, Name = "SegmentBuilder - Support",                       ActivityType = "Support",        ApplicationId = (int?)9,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 16, Name = "PanelIQ - Support",                              ActivityType = "Support",        ApplicationId = (int?)18, ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 17, Name = "P05 ML Forecasting Platform - Project",          ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)5,  Division = "Analytics" },
            new { Id = 18, Name = "P06 Real-Time Analytics Initiative - Project",   ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)6,  Division = "Analytics" },
            new { Id = 19, Name = "P07 SegmentBuilder Enhancement - Project",       ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)7,  Division = "Analytics" },
            new { Id = 20, Name = "FieldOps - Support",                             ActivityType = "Support",        ApplicationId = (int?)10, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 21, Name = "WebCollect - Support",                           ActivityType = "Support",        ApplicationId = (int?)11, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 22, Name = "SampleRouter - Support",                         ActivityType = "Support",        ApplicationId = (int?)12, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 23, Name = "CallCentre Suite - Support",                     ActivityType = "Support",        ApplicationId = (int?)13, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 24, Name = "DiaryTracker - Support",                         ActivityType = "Support",        ApplicationId = (int?)14, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 25, Name = "P08 CallCentre Suite Replacement - Project",     ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)8,  Division = "Data Collection" },
            new { Id = 26, Name = "P09 DiaryTracker Mobile Rewrite - Project",      ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)9,  Division = "Data Collection" },
            new { Id = 27, Name = "P10 Panel Automation Programme - Project",       ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)10, Division = "Data Collection" },
            new { Id = 28, Name = "ClientPortal - Support",                         ActivityType = "Support",        ApplicationId = (int?)15, ProjectId = (int?)null, Division = "Client Services" },
            new { Id = 29, Name = "ReportFactory - Support",                        ActivityType = "Support",        ApplicationId = (int?)16, ProjectId = (int?)null, Division = "Client Services" },
            new { Id = 30, Name = "ReportFactory - Minor Changes",                  ActivityType = "Minor Changes",  ApplicationId = (int?)16, ProjectId = (int?)null, Division = "Client Services" },
            new { Id = 31, Name = "DeliveryTracker - Support",                      ActivityType = "Support",        ApplicationId = (int?)17, ProjectId = (int?)null, Division = "Client Services" },
            new { Id = 32, Name = "P11 Client Onboarding Automation - Project",     ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)11, Division = "Client Services" },
            new { Id = 33, Name = "P12 API Delivery Programme - Project",           ActivityType = "Project",        ApplicationId = (int?)null, ProjectId = (int?)12, Division = "Client Services" },
            new { Id = 34, Name = "APIGateway - Minor Changes",                     ActivityType = "Minor Changes",  ApplicationId = (int?)2,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 35, Name = "SurveyEngine - Minor Changes",                   ActivityType = "Minor Changes",  ApplicationId = (int?)3,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 36, Name = "AuthService - Minor Changes",                    ActivityType = "Minor Changes",  ApplicationId = (int?)4,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 37, Name = "ObservabilityStack - Minor Changes",             ActivityType = "Minor Changes",  ApplicationId = (int?)5,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 38, Name = "DataMapper - Minor Changes",                     ActivityType = "Minor Changes",  ApplicationId = (int?)7,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 39, Name = "QuotaManager - Minor Changes",                   ActivityType = "Minor Changes",  ApplicationId = (int?)8,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 40, Name = "SegmentBuilder - Minor Changes",                 ActivityType = "Minor Changes",  ApplicationId = (int?)9,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 41, Name = "PanelIQ - Minor Changes",                        ActivityType = "Minor Changes",  ApplicationId = (int?)18, ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 42, Name = "FieldOps - Minor Changes",                       ActivityType = "Minor Changes",  ApplicationId = (int?)10, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 43, Name = "WebCollect - Minor Changes",                     ActivityType = "Minor Changes",  ApplicationId = (int?)11, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 44, Name = "SampleRouter - Minor Changes",                   ActivityType = "Minor Changes",  ApplicationId = (int?)12, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 45, Name = "CallCentre Suite - Minor Changes",               ActivityType = "Minor Changes",  ApplicationId = (int?)13, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 46, Name = "DiaryTracker - Minor Changes",                   ActivityType = "Minor Changes",  ApplicationId = (int?)14, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 47, Name = "ClientPortal - Minor Changes",                   ActivityType = "Minor Changes",  ApplicationId = (int?)15, ProjectId = (int?)null, Division = "Client Services" },
            new { Id = 48, Name = "DeliveryTracker - Minor Changes",                ActivityType = "Minor Changes",  ApplicationId = (int?)17, ProjectId = (int?)null, Division = "Client Services" }
        );
}
