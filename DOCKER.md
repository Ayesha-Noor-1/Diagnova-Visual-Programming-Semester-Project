# Docker Setup Guide — Diagnova

Step-by-step instructions to run Diagnova in Docker with **SQL Server**, **MongoDB**, and the **web app**.

---

## Prerequisites

1. **Docker Desktop** installed and running  
   - Download: https://www.docker.com/products/docker-desktop/
2. **OpenAI / OpenRouter API key** (for chat and daily tips)
3. At least **4 GB RAM** free for Docker (SQL Server needs ~2 GB)

Verify Docker works:

```powershell
docker --version
docker compose version
```

---

## Step 1 — Open the project folder

```powershell
cd "C:\Users\T L S\Downloads\Diagnova-Visual-Programming-Semester-Project-improved-v2"
```

You should see:

- `Dockerfile`
- `docker-compose.yml`
- `Diagnova/` folder

---

## Step 2 — Create your environment file

Copy the example env file and edit it:

```powershell
copy .env.example .env
notepad .env
```

Fill in at minimum:

| Variable | Example | Required |
|----------|---------|----------|
| `MSSQL_SA_PASSWORD` | `YourStrong@Passw0rd` | Yes |
| `OPENAI_API_KEY` | `sk-or-v1-...` | Yes (for AI chat) |
| `SMTP_USER` / `SMTP_PASSWORD` | Gmail app password | No (for SOS email) |

**Password rules for SQL Server:** 8+ characters, upper, lower, number, and symbol.

> Never commit `.env` to GitHub — it is listed in `.gitignore`.

---

## Step 3 — Build and start all containers

```powershell
docker compose up --build
```

First run takes **5–15 minutes** (downloads .NET, SQL Server, MongoDB images and builds the app).

When you see:

```text
diagnova-web  | Now listening on: http://[::]:8080
```

the app is ready.

---

## Step 4 — Open the application

In your browser:

**http://localhost:8080**

- Register a new account
- Login and use Dashboard, Chat, SOS, etc.

---

## Step 5 — Stop the containers

Press `Ctrl+C` in the terminal, then:

```powershell
docker compose down
```

To stop **and delete database data**:

```powershell
docker compose down -v
```

---

## What gets created?

| Container | Image | Port | Purpose |
|-----------|-------|------|---------|
| `diagnova-web` | Built from `Dockerfile` | **8080** | ASP.NET Core app |
| `diagnova-sql` | SQL Server 2022 | 1433 | Identity / login |
| `diagnova-mongo` | MongoDB 7 | 27017 | Profiles, chats, vitals |

Data persists in Docker volumes: `sqlserver_data`, `mongo_data`.

---

## Run in background (detached)

```powershell
docker compose up --build -d
docker compose logs -f diagnova
```

Stop:

```powershell
docker compose down
```

---

## Useful commands

```powershell
# Rebuild only the web app after code changes
docker compose up --build diagnova

# View running containers
docker ps

# View web app logs
docker compose logs diagnova -f

# Shell into web container (debugging)
docker exec -it diagnova-web bash

# Remove everything including volumes
docker compose down -v
```

---

## Troubleshooting

### SQL Server fails to start

- Check password in `.env` meets complexity rules.
- Ensure Docker Desktop has enough memory (Settings → Resources → 4 GB+).

### Web container exits / migration errors

- Wait for SQL healthcheck (app retries migrations up to ~60 seconds).
- Check logs: `docker compose logs diagnova`

### AI chat not working

- Verify `OPENAI_API_KEY` in `.env`.
- Restart: `docker compose up --build -d`

### Port 8080 already in use

Edit `docker-compose.yml`:

```yaml
ports:
  - "9090:8080"   # use http://localhost:9090
```

### SOS email not sending

- Set `SMTP_USER` and `SMTP_PASSWORD` in `.env`.
- Email is optional; other features work without it.

---

## How it works (architecture)

```text
Browser → localhost:8080 → diagnova-web (ASP.NET Core)
                              ├── sqlserver:1433  (Identity)
                              └── mongodb:27017     (health data)
```

Configuration is injected via **environment variables** in `docker-compose.yml` (overrides `appsettings.Docker.json`).

---

## Production notes

This setup is for **development / demo**. For production you would also:

- Use HTTPS with a reverse proxy (nginx, Traefik)
- Store secrets in a vault, not `.env`
- Use managed SQL Server / MongoDB Atlas
- Add resource limits and monitoring

---

## Files added for Docker

| File | Purpose |
|------|---------|
| `Dockerfile` | Multi-stage build for Diagnova |
| `docker-compose.yml` | Orchestrates web + SQL + MongoDB |
| `.dockerignore` | Keeps image size small |
| `.env.example` | Template for secrets |
| `Diagnova/appsettings.Docker.json` | Docker-specific defaults |

---

<p align="center">Questions? See the main <a href="Readme.md">Readme.md</a>.</p>
