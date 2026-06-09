# Diagnova

**AI-Powered Health Companion Web Application**  
Visual Programming Semester Project · ASP.NET Core · MongoDB · OpenAI

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Razor%20Pages-512BD4)](https://learn.microsoft.com/aspnet/core/)
[![MongoDB](https://img.shields.io/badge/MongoDB-Database-47A248?logo=mongodb&logoColor=white)](https://www.mongodb.com/)
[![License](https://img.shields.io/badge/License-Academic-blue)](#)

> **Disclaimer:** Diagnova is an educational project for demonstration purposes only. It does **not** provide medical diagnosis, prescriptions, or emergency services. Always consult qualified healthcare professionals for medical decisions. In an emergency, call your local emergency number (e.g. **1122** in Pakistan).

---

## Overview

**Diagnova** is a full-stack web application that helps registered users:

- Describe symptoms and receive **AI-guided health information**
- Search **FDA-approved** medicine labels
- Track **custom vitals** with charts
- Manage a structured **medical profile**
- Get **daily condition-specific health tips**
- Access **emergency support** via keyword detection and **One-Tap SOS**

The platform combines conversational AI with persistent health data, location-aware emergency workflows, and a modern dashboard UI.

**Repository:** [github.com/Ayesha-Noor-1/Diagnova-Visual-Programming-Semester-Project](https://github.com/Ayesha-Noor-1/Diagnova-Visual-Programming-Semester-Project)  
**Active branch:** [`improved-v2`](https://github.com/Ayesha-Noor-1/Diagnova-Visual-Programming-Semester-Project/tree/improved-v2)

---

## Features

### Core modules

| Feature | Description |
|--------|-------------|
| **AI Symptom Assistant** | Natural-language chat powered by OpenAI-compatible API with medical system prompts |
| **Conversation Memory** | Chat sessions and messages stored in MongoDB and sent back as AI context |
| **Specialist Suggestions** | AI recommends relevant specialist types based on reported symptoms |
| **Medicine Lookup** | OpenFDA drug label search (purpose, dosage, warnings, adverse reactions) |
| **Vitals Tracker** | User-defined vitals with readings and Chart.js trend graphs |
| **Health Dashboard** | Stats, activity chart, sidebar navigation, and module hub |
| **Medical Profile (Artifacts)** | Editable profile: conditions, allergies, medications, emergency contacts |
| **Multi-Step Registration** | 5-step onboarding with live username/email availability checks |

### Safety & emergency

| Feature | Description |
|--------|-------------|
| **Emergency Keyword Detector** | Local scan for urgent phrases (e.g. chest pain, stroke) before AI processing |
| **Emergency Modal** | Instant alert with helpline **1122**, Leaflet map, and nearby hospitals |
| **One-Tap SOS** | Floating SOS button sends GPS + condensed medical profile to emergency contacts via email |
| **Emergency Email Alerts** | SMTP notifications to configured emergency contacts during critical chat events |

### Personalization

| Feature | Description |
|--------|-------------|
| **Daily Condition Tips** | One AI-generated tip per day (diet / exercise / lifestyle) based on registered conditions, cached in MongoDB |
| **Profile-Aware AI** | Chat uses stored allergies, medications, and conditions for personalized responses |

---

## Tech Stack

| Layer | Technologies |
|-------|--------------|
| **Backend** | ASP.NET Core 10, Razor Pages, Minimal APIs |
| **Authentication** | ASP.NET Core Identity, Entity Framework Core |
| **Auth database** | SQL Server (LocalDB) |
| **Application database** | MongoDB |
| **AI** | OpenAI-compatible API (OpenRouter / GPT-4o-mini) |
| **Medicine API** | OpenFDA |
| **Maps / Hospitals** | OpenStreetMap, Overpass API, Nominatim, Leaflet |
| **Email** | MailKit / MimeKit (SMTP) |
| **Frontend** | Bootstrap 5, JavaScript, Chart.js |
| **Markdown** | Markdig |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation                                               │
│  Razor Pages · Dashboard (JS) · chat.js · sos.js            │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│  Application Services                                       │
│  OpenAiChatService · OpenFdaService · SosAlertService       │
│  DailyTipService · EmergencyPlacesService · EmailService    │
│  EmergencyDetectorService                                   │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│  Data                                                       │
│  SQL Server (Identity)  │  MongoDB (profiles, chats, vitals)│
└─────────────────────────────────────────────────────────────┘
```

### MongoDB collections

- `MedicalProfiles` — user health profiles
- `ChatSessions` / `ChatMessages` — conversation history
- `VitalDefinitions` — vitals with embedded readings
- `MedicineSearchHistory` — past drug searches
- `DailyHealthTips` — one cached tip per user per day

---

## Project Structure

```
Diagnova-Visual-Programming-Semester-Project/
├── Diagnova.sln
├── Readme.md
└── Diagnova/
    ├── Areas/Identity/       # Login & registration
    ├── Data/                 # EF Core DbContext (Identity)
    ├── Models/               # Domain & MongoDB documents
    ├── Pages/                # Razor Pages UI
    ├── Services/               # Business logic & external APIs
    ├── Migrations/             # SQL Server Identity migrations
    ├── wwwroot/                # CSS, JS, static assets
    └── Program.cs              # Startup & REST endpoints
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server Express LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb) (or SQL Server instance)
- [MongoDB](https://www.mongodb.com/) (local or Atlas)
- OpenAI-compatible API key (OpenAI or [OpenRouter](https://openrouter.ai/))
- SMTP credentials (optional — for SOS / emergency emails)

### Installation

```bash
# Clone the repository
git clone https://github.com/Ayesha-Noor-1/Diagnova-Visual-Programming-Semester-Project.git
cd Diagnova-Visual-Programming-Semester-Project
git checkout improved-v2

# Navigate to the project
cd Diagnova

# Copy configuration template
copy appsettings.example.json appsettings.json   # Windows
# cp appsettings.example.json appsettings.json   # Linux/macOS
```

### Configuration

Edit `Diagnova/appsettings.json` or use **User Secrets** (recommended):

```bash
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key-here"
dotnet user-secrets set "ConnectionStrings:MongoDbConnection" "mongodb://localhost:27017"
dotnet user-secrets set "Email:SmtpUser" "your-email@example.com"
dotnet user-secrets set "Email:SmtpPassword" "your-app-password"
```

**OpenAI / OpenRouter example:**

```json
{
  "OpenAI": {
    "BaseUrl": "https://openrouter.ai/api/v1/",
    "Model": "openai/gpt-4o-mini",
    "EmergencyRoutingModel": "openai/gpt-4o-mini"
  }
}
```

> **Security:** Never commit real API keys, MongoDB credentials, or SMTP passwords. `appsettings.json` is gitignored.

### Run the application

```bash
dotnet restore
dotnet run
```

Open in browser:

- HTTPS: `https://localhost:7010`
- HTTP: `http://localhost:5050`

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/vitals` | List user vitals |
| `POST` | `/api/vitals` | Create a vital |
| `POST` | `/api/vitals/reading` | Add a reading |
| `GET` | `/api/medicine/search?query=` | Search OpenFDA drug labels |
| `GET` | `/api/medicine/history` | Medicine search history |
| `GET` | `/api/chat/sessions` | List chat sessions |
| `GET` | `/api/tips/daily` | Today's condition-specific tip |
| `GET` | `/api/dashboard/activity` | 7-day message activity |
| `POST` | `/api/profile/update` | Update profile field |
| `POST` | `/api/sos/trigger` | One-Tap SOS alert |
| `POST` | `/api/emergency/send-email` | Emergency contact email |
| `GET` | `/api/check-username` | Username availability |
| `GET` | `/api/check-email` | Email availability |

All endpoints except availability checks require authentication.

---

## Key User Flows

1. **Register** → complete 5-step health profile → **Login**
2. **Dashboard** → view daily tip, stats, and navigate modules
3. **Symptom Assistant** → chat with AI (history preserved per session)
4. **Emergency** → keyword in chat triggers modal, or tap **SOS** for instant alert
5. **Medicine** → search drug → view FDA label information
6. **Vitals** → create metrics → log readings → view charts
7. **Artifacts** → view/edit medical profile and emergency contacts

---

## Team & Contributions

| Contributor | Role | Focus areas |
|-------------|------|-------------|
| **Ayesha Noor** | Backend & integrations | AI chat, MongoDB, REST APIs, emergency logic, SOS, daily tips, OpenFDA, OpenRouter |
| **Ayesha Riaz** | Frontend & UI/UX | Landing page, auth screens, dashboard layout, UI polish, registration/chat UI |

---

## Screenshots

<!-- Add screenshots when available -->
| Landing | Dashboard | AI Chat |
|---------|-----------|---------|
| _screenshot_ | _screenshot_ | _screenshot_ |

| SOS | Medicine | Vitals |
|-----|----------|--------|
| _screenshot_ | _screenshot_ | _screenshot_ |

---

## Roadmap

- [ ] Voice input (Azure Speech SDK)
- [ ] PDF export of chat history
- [ ] Enhanced Google Places hospital integration
- [ ] Automated test suite
- [ ] Progressive Web App (PWA) support

---

## Academic Use

This project was developed as a **Visual Programming semester project** to demonstrate:

- Full-stack web development with ASP.NET Core
- Integration of third-party APIs (AI, FDA, maps)
- Dual-database architecture (SQL + NoSQL)
- User authentication and data isolation
- Real-world feature design (emergency workflows, personalization)

---

## License

This project is submitted for **academic evaluation**. All rights reserved by the authors. Not licensed for commercial medical use.

---

## Contact

- **GitHub:** [Ayesha-Noor-1](https://github.com/Ayesha-Noor-1)
- **Repository:** [Diagnova-Visual-Programming-Semester-Project](https://github.com/Ayesha-Noor-1/Diagnova-Visual-Programming-Semester-Project)

---

<p align="center">
· Diagnova © 2026
</p>
