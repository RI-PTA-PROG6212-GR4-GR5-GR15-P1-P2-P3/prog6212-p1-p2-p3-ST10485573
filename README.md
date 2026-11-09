# RaceDay API (Part 3)

This project implements the Part 2 RESTful API for the RaceDay event management system. It includes:

- User registration and login with JWT authentication
- Role-based access control for Organisers and Participants
- Event management endpoints
- Event category management
- Event enrolment workflows
- Result capture and retrieval
- Swagger UI for API exploration and validation
- Unit tests for expected success and failure flows

## Roles

- Organiser: can create, update, and delete events; manage event categories; view all enrolments; capture results
- Participant: can register/login, view events, enrol in a selected category, view own enrolments, and view personal results

## Run locally

```bash
dotnet restore ./PROG_API/PROG_API.csproj
dotnet run --project ./PROG_API/PROG_API.csproj
```

Then open:

- Swagger UI: https://localhost:7289/swagger
- API base: https://localhost:7289/api

### Image storage

Development uses the API's local `PROG_API/BlobStorage` folder by default. To store event banners and profile pictures in Azure Blob Storage, provide the storage account connection string to the API as `BlobStorage__ConnectionString` (for example, through a user secret or environment variable). Keep the connection string out of source control. When configured, the API writes images to private `event-banners` and `profile-pictures` containers and serves them through its `/api/blob/{container}/{fileName}` route; the MVC application only calls the API.

Production deployments must configure `BlobStorage__ConnectionString`; the API fails fast rather than silently saving uploads to ephemeral local disk. For Docker Compose, set `AZURE_STORAGE_CONNECTION_STRING` in the environment or a local `.env` file. Set `Api__PublicBaseUrl` to the API URL reachable by the browser when the MVC app and API are deployed on different hosts. The compose file uses `http://localhost:5001/` for its local published API port.

## Run tests

```bash
dotnet test ./PROG_Test/PROG_Test.csproj
```

## Run MVC web app

```bash
dotnet run --project ./PROG_MVC/PROG_MVC.csproj
```

## Run with Docker

```bash
docker build -t raceday-web -f ./PROG_MVC/Dockerfile .
docker run -p 8080:8080 raceday-web
```

## Default seed users

- organiser@raceday.co.za / Password123!
- participant@raceday.co.za / Password123!

## CI/CD

![CI Build](docs/ci-build.png)

## Video Presentation

YouTube: https://www.youtube.com/watch?v=YOUR_VIDEO_ID

## References

- Microsoft Learn. (2026). *Add JWT Bearer authentication in ASP.NET Core*. https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt
- Microsoft Learn. (2026). *Create web APIs with ASP.NET Core*. https://learn.microsoft.com/en-us/aspnet/core/web-api/
- Microsoft Learn. (2026). *Data annotations for model validation*. https://learn.microsoft.com/en-us/aspnet/core/mvc/models/data-annotations
- Microsoft Learn. (2026). *EF Core Code-First Development*. https://learn.microsoft.com/en-us/ef/core/modeling/
- Microsoft Learn. (2026). *Integration tests in ASP.NET Core*. https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
- Microsoft Learn. (2026). *Model validation in ASP.NET Core MVC*. https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation
- Microsoft Learn. (2026). *Data Seeding - EF Core*. https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding/
- OWASP Foundation. (2026). *Password Storage Cheat Sheet*. https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html

## Notes

The API is configured to use SQL Server when a real connection string is supplied, and falls back to EF Core in-memory storage for local testing and CI when `UseInMemoryDatabase` is enabled. This keeps the project executable in both development and automated environments.

To switch to SQL Server, set `UseInMemoryDatabase=false` and update the `ConnectionStrings:DefaultConnection` value in `PROG_API/appsettings.json`.
