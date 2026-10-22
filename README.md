# FinOps Multi-Agent Fraud & Risk Engine

A high-throughput, low-latency financial operations (FinOps) platform built with **.NET 8 Clean Architecture**, **Angular**, **Semantic Kernel**, and **ONNX Runtime**. This platform performs real-time fraud detection, sub-millisecond sliding-window feature aggregation, automated Suspicious Activity Report (SAR) generation using LLM multi-agent coordination, and full compliance audit logging.

---

## Tech Stack Icons

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Angular](https://img.shields.io/badge/Angular-18-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.0-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Redis](https://img.shields.io/badge/Redis-Cache-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![DynamoDB](https://img.shields.io/badge/DynamoDB-Local-4053D6?logo=amazondynamodb&logoColor=white)](https://aws.amazon.com/dynamodb/)
[![SQLite](https://img.shields.io/badge/SQLite-Audit-003B57?logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![Semantic Kernel](https://img.shields.io/badge/Semantic%20Kernel-AI-0078D4?logo=microsoft&logoColor=white)](https://github.com/microsoft/semantic-kernel)
[![OpenAI](https://img.shields.io/badge/OpenAI-GPT--4o-412991?logo=openai&logoColor=white)](https://platform.openai.com/)
[![Docker](https://img.shields.io/badge/Docker-Containers-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## Architecture Diagram

```
                             +-----------------------+
                             |    Angular Frontend    |
                             |   (SignalR Dashboard) |
                             +-----------^-----------+
                                         | (WebSockets)
                                         |
+----------------------------------------v----------------------------------------+
| FinOps Risk Engine API (.NET 8 Clean Architecture)                               |
|                                                                                 |
|   +-------------------+    +--------------------+    +----------------------+   |
|   | ONNX Model        |    | Sliding Window     |    | Multi-Agent Risk    |   |
|   | Evaluator         |    | Feature Store      |    | Coordinator          |   |
|   | (< 1ms Inference) |    | (Redis + DynamoDB) |    | (Semantic Kernel)    |   |
|   +---------+---------+    +---------+----------+    +----------+-----------+   |
|             |                        |                          |               |
+-------------|------------------------|--------------------------|---------------+
              |                        |                          |
              v                        v                          v
     +-----------------+      +------------------+      +-------------------+
     | ONNX Runtime    |      | Redis Cache &    |      | SQLite / Postgres |
     | (fraud_model)   |      | DynamoDB Local   |      | (Compliance Audit)|
     +-----------------+      +------------------+      +-------------------+
```

---

## Features

- **Sub-Millisecond Fraud Scoring:** Embedded ONNX runtime (`fraud_model.onnx`) evaluates transactions in `< 1ms` locally without external network overhead.
- **Dual-Layer Sliding Window Feature Store:** 
  - **In-Memory (Redis):** Tracks ultra-low latency real-time sliding windows.
  - **Persistent Store (DynamoDB Local):** Long-term feature aggregation across account window buckets.
- **Multi-Agent Risk Coordinator:** Uses Microsoft Semantic Kernel to orchestrate LLM agents for automated Suspicious Activity Report (SAR) generation and risk evaluation.
- **Real-Time Web Push:** Broadcasts high-risk transaction alerts instantly to the Angular dashboard via SignalR WebSockets.
- **Compliance & Audit Logging:** Tracks agent plugin invocations and function execution metrics in an append-only audit database (SQLite locally, scalable to PostgreSQL in production).
- **Resilient Background Processing:** Built-in fault tolerance with timeout guards preventing thread exhaustion and event-loop starvation across container networks.

---

## Tech Stack

| Layer | Technology |
| :--- | :--- |
| **Backend Framework** | .NET 8 Web API (Clean Architecture: Domain, Core, Infrastructure, Api) |
| **Frontend Framework** | Angular 17+ with TailwindCSS & SignalR Client |
| **Machine Learning** | Microsoft.ML.OnnxRuntime |
| **AI Orchestration** | Microsoft Semantic Kernel |
| **Caching / Fast Store** | Redis (`StackExchange.Redis`) |
| **Persistent Feature Store**| Amazon DynamoDB (AWS SDK v4) / DynamoDB Local |
| **Audit Storage** | SQLite / EF Core (Production target: PostgreSQL / Cloud SQL) |
| **Telemetry & Tracing** | OpenTelemetry |
| **Containerization** | Docker, Docker Compose |

---

## Quick Start & Local Setup

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (ensure Docker compose is enabled)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js (v18+)](https://nodejs.org/) & Angular CLI (`npm i -g @angular/cli`)

### Environment Configuration

1. Clone the repository:
   ```bash
   git clone https://github.com/your-username/finops-risk-engine.git
   cd finops-risk-engine
   ```

2. Configure environment variables or secrets:
   Set your OpenAI API Key using .NET User Secrets (recommended for development):
   ```bash
   dotnet user-secrets set "OpenAI:ApiKey" "your-actual-openai-api-key" --project FinOps.Api
   ```
   *Alternatively, create a `.env` file in the root directory.*

### Running the Stack with Docker Compose

Spin up the entire multi-container environment (API, Redis, and DynamoDB Local):

```bash
docker compose up -d --build
```

Verify all services are running:
```bash
docker compose ps
```

Check API container logs:
```bash
docker logs -f finops-risk-api
```

### Running the Angular Dashboard

1. Navigate to the frontend directory:
   ```bash
   cd src/FinOps.UI
   ```

2. Install dependencies and start the development server:
   ```bash
   npm install
   ng serve
   ```

3. Open your browser and navigate to `http://localhost:4200`. The connection indicator will turn green once connected to `http://localhost:5001/hubs/risk`.

---

## License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.