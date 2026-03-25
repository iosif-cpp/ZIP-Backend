using Kaspersky_Task1.Background;
using Kaspersky_Task1.Configuration;
using Kaspersky_Task1.Services.Builders;
using Kaspersky_Task1.Services.Caches;
using Kaspersky_Task1.Services.Catalogs;
using Kaspersky_Task1.Services.Stores;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using NpgsqlTypes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Kaspersky_Task1.Validation.InitArchiveRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "Kaspersky Task1 API", Version = "v1" });
});

var filesDirRel = builder.Configuration["Archive:FilesDir"] ?? "files";
var zipArchivesDirRel = builder.Configuration["Archive:ZipArchivesDir"] ?? "ZipArchives";
var maxFiles = builder.Configuration.GetValue<int?>("Archive:MaxFilesPerArchive") ?? 1000;

var archiveOptions = new ArchiveOptions
{
    FilesDir = Path.Combine(builder.Environment.ContentRootPath, filesDirRel),
    ZipArchivesDir = Path.Combine(builder.Environment.ContentRootPath, zipArchivesDirRel),
    MaxFilesPerArchive = maxFiles
};

builder.Services.AddSingleton(archiveOptions);
builder.Services.AddSingleton<IArchiveJobStore, InMemoryArchiveJobStore>();
builder.Services.AddSingleton<IFileCatalog, PhysicalFileCatalog>();
builder.Services.AddSingleton<IArchiveCache, ArchiveCache>();
builder.Services.AddSingleton<IArchiveBuilder, ZipArchiveBuilder>();

builder.Services.AddSingleton<System.Threading.Channels.Channel<Guid>>(_ =>
    System.Threading.Channels.Channel.CreateUnbounded<Guid>(new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true }));

builder.Services.AddHostedService<ArchiveBackgroundWorker>();

var postgresConnectionString = builder.Configuration.GetConnectionString("PostgresLogging");
var postgresTableName = builder.Configuration["Serilog:Postgres:TableName"] ?? "http_request_logs";
var postgresNeedAutoCreateTable = builder.Configuration.GetValue<bool?>("Serilog:Postgres:NeedAutoCreateTable") ?? true;

if (!string.IsNullOrWhiteSpace(postgresConnectionString))
{
    var columnWriters = new Dictionary<string, ColumnWriterBase>
    {
        { "message", new RenderedMessageColumnWriter(NpgsqlDbType.Text) },
        { "message_template", new MessageTemplateColumnWriter(NpgsqlDbType.Text) },
        { "level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
        { "raise_date", new TimestampColumnWriter(NpgsqlDbType.TimestampTz) },
        { "exception", new ExceptionColumnWriter(NpgsqlDbType.Text) },
        { "properties", new LogEventSerializedColumnWriter(NpgsqlDbType.Jsonb) }
    };

    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.PostgreSQL(
                postgresConnectionString,
                postgresTableName,
                columnWriters,
                needAutoCreateTable: postgresNeedAutoCreateTable,
                period: TimeSpan.FromSeconds(1));
    });
}

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1"));

if (!string.IsNullOrWhiteSpace(postgresConnectionString))
{
    app.UseSerilogRequestLogging();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.MapControllers();
app.Run();