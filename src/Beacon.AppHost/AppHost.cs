var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL — persistent dev volume (password persists via the AppHost UserSecretsId).
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("beacon-pgdata")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin();

var db = postgres.AddDatabase("beacon");

// Redis — cache L2 + FusionCache backplane. Ephemeral is fine for a cache.
var redis = builder.AddRedis("cache");

builder.AddProject<Projects.Beacon_Api>("api")
    .WithReference(db).WaitFor(db)
    .WithReference(redis).WaitFor(redis)
    // Redirects resolve on the API origin in dev; the SPA renders these short URLs.
    .WithEnvironment("ShortUrl__BaseUrl", "http://localhost:34110")
    // Pin the API port so a standalone `npm run dev` (Vite proxy) always finds it.
    .WithEndpoint("http", e => e.Port = 34110);

await builder.Build().RunAsync();
