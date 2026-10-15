# RaceDay API (Part 2)

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

- Swagger UI: https://localhost:5001/swagger
- API base: https://localhost:5001/api

## Run tests

```bash
dotnet test ./PROG_Test/PROG_Test.csproj
```

## Default seed users

- organiser@raceday.co.za / Password123!
- participant@raceday.co.za / Password123!

## Notes

The API is configured to use SQL Server when a real connection string is supplied, and falls back to EF Core in-memory storage for local testing and CI when `UseInMemoryDatabase` is enabled. This keeps the project executable in both development and automated environments.

To switch to SQL Server, set `UseInMemoryDatabase=false` and update the `ConnectionStrings:DefaultConnection` value in `PROG_API/appsettings.json`.
