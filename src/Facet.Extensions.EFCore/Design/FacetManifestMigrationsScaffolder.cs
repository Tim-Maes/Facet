using System.IO;
using Microsoft.EntityFrameworkCore.Migrations.Design;

namespace Facet.Extensions.EFCore.Design;

/// <summary>
/// A <see cref="MigrationsScaffolder"/> that additionally writes the Facet EF model manifest
/// (see <see cref="FacetEfModelManifest"/>) beside the model snapshot whenever a migration is
/// added or removed. Because migrations are the moment the committed snapshot is synchronized
/// with the model, the manifest stays exactly as fresh as the snapshot itself — the same
/// workflow that keeps migrations honest keeps DTO shapes honest.
/// </summary>
public class FacetManifestMigrationsScaffolder : MigrationsScaffolder
{
    public FacetManifestMigrationsScaffolder(MigrationsScaffolderDependencies dependencies)
        : base(dependencies)
    {
    }

#if NET9_0_OR_GREATER
    /// <inheritdoc />
    public override MigrationFiles Save(string projectDir, ScaffoldedMigration migration, string? outputDir, bool dryRun)
    {
        var files = base.Save(projectDir, migration, outputDir, dryRun);
        if (!dryRun)
        {
            WriteManifestBeside(files);
        }

        return files;
    }

    /// <inheritdoc />
    public override MigrationFiles RemoveMigration(string projectDir, string? rootNamespace, bool force, string? language, bool dryRun)
    {
        var files = base.RemoveMigration(projectDir, rootNamespace, force, language, dryRun);
        if (!dryRun)
        {
            WriteManifestBeside(files);
        }

        return files;
    }
#else
    /// <inheritdoc />
    public override MigrationFiles Save(string projectDir, ScaffoldedMigration migration, string? outputDir)
    {
        var files = base.Save(projectDir, migration, outputDir);
        WriteManifestBeside(files);
        return files;
    }

    /// <inheritdoc />
    public override MigrationFiles RemoveMigration(string projectDir, string? rootNamespace, bool force, string? language)
    {
        var files = base.RemoveMigration(projectDir, rootNamespace, force, language);
        WriteManifestBeside(files);
        return files;
    }
#endif

    private void WriteManifestBeside(MigrationFiles files)
    {
        // Removing the last migration deletes the snapshot without re-scaffolding one; with no
        // snapshot there is no directory the manifest belongs beside, so leave it untouched.
        var directory = files.SnapshotFile is { } snapshotFile ? Path.GetDirectoryName(snapshotFile) : null;
        if (directory == null)
        {
            return;
        }

        var contextName = Dependencies.CurrentContext.Context.GetType().Name;
        FacetEfModelManifest.Write(Dependencies.Model, directory, contextName);
    }
}
